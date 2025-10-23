namespace Bot.Fsm;

public enum FsmState
{
    None = 0,
    ImportAwaitFile = 1,
    EditAwaitDate = 2,
    EditAwaitText = 3,
    SetTimeAwaitDate = 4,
    SetTimeAwaitValue = 5
}
