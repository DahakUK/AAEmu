﻿using System;
using System.Collections.Generic;
using AAEmu.Commons.Exceptions;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Mails;

namespace AAEmu.Game.Models.Game.Char;

public class CharacterMails
{
    public Character Self { get; set; }
    public CountUnreadMail unreadMailCount;

    public CharacterMails(Character self)
    {
        Self = self;

        unreadMailCount = new CountUnreadMail
        {
            Sent = 0,
        };
        unreadMailCount.ResetReceived();
    }

    public void OpenMailbox()
    {
        var total = 0;
        foreach (var m in MailManager.Instance.GetCurrentMailList(Self.Id))
        {
            if (m.Value.Header.SenderId == Self.Id && m.Value.Header.ReceiverId == Self.Id)
            {
                Self.SendPacket(new SCMailListPacket(false, [m.Value.Header]));
                total++;
            }
            else if (m.Value.Header.SenderId == Self.Id)
            {
                Self.SendPacket(new SCMailListPacket(true, [m.Value.Header]));
                total++;
            }
            else if (m.Value.Header.ReceiverId == Self.Id)
            {
                Self.SendPacket(new SCMailListPacket(false, [m.Value.Header]));
                total++;
            }
        }
        Self.SendPacket(new SCMailListEndPacket(total, 0));
    }

    public void ReadMail(bool isSent, long id)
    {
        if (MailManager.Instance._allPlayerMails.TryGetValue(id, out var mail))
        {
            if ((mail.Header.Status == MailStatus.Unread) && !isSent)
            {
                unreadMailCount.UpdateReceived(mail.MailType, -1);
                mail.OpenDate = DateTime.UtcNow;
                mail.Header.Status = MailStatus.Read;
                mail.IsDelivered = true;
            }
            Self.SendPacket(new SCMailBodyPacket(false, isSent, mail.Body, true, unreadMailCount));
            Self.SendPacket(new SCMailStatusUpdatedPacket(isSent, id, mail.Header.Status));
            SendUnreadMailCount();
        }
    }

    public void SendUnreadMailCount()
    {
        Self.SendPacket(new SCCountUnreadMailPacket(unreadMailCount));
    }

    public MailResult SendMailToPlayer(MailType mailType, string receiverName, string title, string text, byte attachments, int money0, int money1, int money2, long extra, List<(SlotType, byte)> itemSlots)
    {

        if (string.IsNullOrWhiteSpace(receiverName) || NameManager.Instance.GetCharacterId(receiverName) == 0)
        {
            return MailResult.UnableToFindRecipient;
        }

        var mail = new MailPlayerToPlayer(Self, receiverName);

        mail.MailType = mailType;
        mail.Title = title;

        mail.Header.Attachments = attachments;
        mail.Header.Extra = extra;

        mail.Body.Text = text;
        mail.Body.SendDate = DateTime.UtcNow;
        mail.Body.RecvDate = DateTime.UtcNow;

        mail.AttachMoney(money0, money1, money2);

        // First verify source items, and add them to the attachments of body
        if (!mail.PrepareAttachmentItems(itemSlots))
        {
            // Self.SendErrorMessage(ErrorMessageType.MailInvalidItem);
            return MailResult.InvalidSlot;
        }

        // With attachments in place, we can calculate the send fee
        var mailFee = mail.GetMailFee();
        if ((mailFee + money0) > Self.Money)
        {
            // Self.SendErrorMessage(ErrorMessageType.MailNotEnoughMoney);
            return MailResult.InsufficientCoins;
        }

        if (!mail.FinalizeAttachments())
            return MailResult.InvalidSlot; // Should never fail at this point

        // Add delay if not a normal snail mail
        if (mailType == MailType.Normal)
            mail.Body.RecvDate = DateTime.UtcNow + MailManager.NormalMailDelay;

        // Send it
        if (mail.Send())
        {
            Self.SendPacket(new SCMailSentPacket(mail.Header, itemSlots.ToArray()));
            // Take the fee
            Self.SubtractMoney(SlotType.Inventory, mailFee + money0);
            return MailResult.Success;
        }
        else
        {
            return MailResult.MailErrorOccurred;
        }
    }

    public bool GetAttached(long mailId, bool takeMoney, bool takeItems, bool takeAllSelected, ulong specifiedItemId = 0)
    {
        var res = true;
        if (MailManager.Instance._allPlayerMails.TryGetValue(mailId, out var thisMail))
        {
            bool tookMoney = false;
            if ((thisMail.MailType == MailType.AucOffSuccess) && (thisMail.Body.CopperCoins > 0) && takeMoney)
            {
                if (Self.LaborPower < 1)
                {
                    Self.SendErrorMessage(ErrorMessageType.NotEnoughLaborPower);
                    takeMoney = false;
                }
                else
                {
                    Self.ChangeLabor(-1, (int)ActabilityType.Commerce);
                }
            }
            if (thisMail.Body.CopperCoins > 0 && takeMoney)
            {
                Self.ChangeMoney(SlotType.Inventory, thisMail.Body.CopperCoins);
                thisMail.Body.CopperCoins = 0;
                thisMail.Header.Attachments -= 1;
                tookMoney = true;
            }

            var itemSlotList = new List<ItemIdAndLocation>();
            // Check if items need to be taken, and add them to a list
            if (takeItems)
            {
                var toRemove = new List<Item>();
                foreach (var itemAttachment in thisMail.Body.Attachments)
                {
                    // if not our specified item, skip this slot
                    if ((specifiedItemId > 0) && (itemAttachment.Id != specifiedItemId))
                        continue;

                    // Sanity-check
                    if (itemAttachment.Id != 0)
                    {
                        // Free Space Check
                        if (Self.Inventory.Bag.SpaceLeftForItem(itemAttachment, out var foundItems) >= itemAttachment.Count)
                        {
                            Item stackItem = null;
                            // Check if we can stack the item onto a existing one
                            if ((itemAttachment.Template.MaxCount > 1) && (foundItems.Count > 0))
                            {
                                foreach (var fi in foundItems)
                                {
                                    if ((fi.Count + itemAttachment.Count) <= fi.Template.MaxCount)
                                    {
                                        stackItem = fi;
                                        break;
                                    }
                                }
                            }

                            var iial = new ItemIdAndLocation();
                            iial.Id = itemAttachment.Id;
                            iial.SlotType = itemAttachment.SlotType;
                            iial.Slot = (byte)itemAttachment.Slot;

                            // Move item to player inventory
                            if (Self.Inventory.Bag.AddOrMoveExistingItem(ItemTaskType.Mail, itemAttachment, stackItem != null ? stackItem.Slot : -1))
                            {
                                itemSlotList.Add(iial);
                                thisMail.Header.Attachments -= 1;
                                toRemove.Add(itemAttachment);
                            }
                            else
                            {
                                // Should technically never fail because of previous free slot check
                                throw new GameException("GetAttachmentFailedAddToBag");
                            }
                        }
                        else
                        {
                            // Bag Full
                            Self.SendErrorMessage(ErrorMessageType.BagFull);
                            res = false;
                        }
                    }
                }
                // Removed those marked to be taken
                foreach (var ia in toRemove)
                    thisMail.Body.Attachments.Remove(ia);

            }
            // Mark taken items

            // Send attachments taken packets (if needed)
            // Money
            if (tookMoney)
            {
                Self.SendPacket(new SCAttachmentTakenPacket(mailId, true, false, takeAllSelected, []));
            }

            // Items
            if (itemSlotList.Count > 0)
            {
                // Self.SendPacket(new SCAttachmentTakenPacket(mailId, takeMoney, false, takeAllSelected, itemSlotList));
                /* 
                 * ZeromusXYZ:
                 * Splitting this packet up to be sent one by one fixes delivery issue in cases where not everything is deliverd at once,
                 * like full bag, manual item grabbing.
                 * It's kind of silly, but I don't have a better solution for it 
                */
                foreach (var iSlot in itemSlotList)
                {
                    var dummyItemSlotList = new List<ItemIdAndLocation>
                    {
                        iSlot
                    };
                    Self.SendPacket(new SCAttachmentTakenPacket(mailId, takeMoney, false, takeAllSelected, dummyItemSlotList));
                }
            }

            // Mark mail as read in case we took at least one item from it
            if ((thisMail.Header.Status == MailStatus.Unread) && (tookMoney || (itemSlotList.Count > 0)))
            {
                thisMail.Header.Status = MailStatus.Read;
                unreadMailCount.UpdateReceived(thisMail.MailType, -1);
                Self.SendPacket(new SCMailStatusUpdatedPacket(false, mailId, MailStatus.Read));
                SendUnreadMailCount();
            }

            // TODO: Make sure attachment settings and mail info is sent back correctly 
            // taking all attachments sometimes doesn't enable the delete button when getting attachments using "GetAllSelected"

            // TODO: if source player is online, update their mail info (sent tab)
        }

        return res;
    }

    public void DeleteMail(long id, bool isSent)
    {
        if (MailManager.Instance._allPlayerMails.ContainsKey(id) && !isSent)
        {
            if (MailManager.Instance._allPlayerMails[id].Header.Attachments <= 0)
            {
                if (MailManager.Instance._allPlayerMails[id].Header.Status != MailStatus.Read)
                {
                    unreadMailCount.UpdateReceived(MailManager.Instance._allPlayerMails[id].MailType, -1);
                    Self.SendPacket(new SCMailDeletedPacket(isSent, id, true, unreadMailCount));
                }
                else
                    Self.SendPacket(new SCMailDeletedPacket(isSent, id, false, unreadMailCount));
                MailManager.Instance.DeleteMail(id);
            }
        }
    }

    public void ReturnMail(long id)
    {
        if (MailManager.Instance._allPlayerMails.TryGetValue(id, out var thisMail))
        {
            var itemSlots = new List<(SlotType slotType, byte slot)>();
            for (var i = 0; i < MailBody.MaxMailAttachments; i++)
            {
                var item = ItemManager.Instance.GetItemByItemId(thisMail.Body.Attachments[i].Id);
                if (item.SlotType == SlotType.None)
                    itemSlots.Add((0, 0));
                else
                    itemSlots.Add((item.SlotType, (byte)item.Slot));
            }

            SendMailToPlayer(thisMail.Header.Type, thisMail.Header.SenderName, thisMail.Header.Title, thisMail.Body.Text,
                thisMail.Header.Attachments, thisMail.Body.CopperCoins, thisMail.Body.BillingAmount, thisMail.Body.MoneyAmount2,
                    thisMail.Header.Extra, itemSlots);

            DeleteMail(id, false);
        }
    }
}
