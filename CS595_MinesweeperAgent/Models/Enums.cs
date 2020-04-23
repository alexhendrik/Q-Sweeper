/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines enums used to improve code readability.
 */


namespace CS595_MinesweeperAgent.Models
{
    public class Enums
    {
        public enum RevealResponse
        {
            Bomb = -1,
            Nothing = 0,
            Success = 1
        }

        public enum GameOutcome
        {
            Loss = 0,
            Win = 1,
            Undefined = 2
        }
    }
}
