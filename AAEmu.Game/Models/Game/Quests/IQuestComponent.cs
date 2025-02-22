﻿namespace AAEmu.Game.Models.Game.Quests;

public interface IQuestComponent
{
    public QuestComponentTemplate Template { get; set; }
    bool OverrideObjectiveCompleted { get; set; }

    /// <summary>
    /// Initialize all Acts in this Component (register event handlers)
    /// </summary>
    public void InitializeComponent();

    /// <summary>
    /// Finalize all Acts in this Component (un-register event handlers)
    /// </summary>
    public void FinalizeComponent();

    /// <summary>
    /// Execute all the acts in this component and return true if successful
    /// </summary>
    /// <returns></returns>
    public bool RunComponent();
}
