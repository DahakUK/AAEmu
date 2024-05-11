using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AAEmu.Game.Models.Game.Items;

public class LootInstance
{
    // Dictionary mapping each player to their respective loot
    private Dictionary<uint, List<Item>> _playerLootItems;

    public LootInstance()
    {
        _playerLootItems = new Dictionary<uint, List<Item>>();
    }

    // Method to add loot for a player
    public void AddLoot(uint playerId, List<Item> items)
    {
        _playerLootItems[playerId] = items;
    }

    // Method to get loot for a player
    public List<Item> GetLoot(uint playerId)
    {
        _playerLootItems.TryGetValue(playerId, out var items);
        return items;
    }

    // Method to remove all loot for a player
    public void RemoveAllPlayerLoot(uint playerId)
    {
        _playerLootItems.Remove(playerId);
    }

    // Method to remove a specific item from a player's loot
    public void RemoveItemFromLoot(uint playerId, Item item)
    {
        if (_playerLootItems.TryGetValue(playerId, out var items))
        {
            items.Remove(item);
        }
    }
}
