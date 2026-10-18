using System.Collections.Generic;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// The choice the player made in one scenario.
    /// </summary>
    public class ScenarioResult
    {
        public ScenarioResult(Scenario scenario, DialogueOption chosen)
        {
            Scenario = scenario;
            Chosen = chosen;
        }

        public Scenario Scenario { get; }
        public DialogueOption Chosen { get; }
        public int Score => Chosen.Score;
        public int BestScore => Scenario.BestScore;
    }

    /// <summary>
    /// Keeps the hidden score of one session in memory. Nothing is saved after the game closes (GAME_BRIEF.md).
    /// </summary>
    public class SessionScore
    {
        private readonly List<ScenarioResult> _results = new List<ScenarioResult>();

        public IReadOnlyList<ScenarioResult> Results => _results;

        public int Total
        {
            get
            {
                var total = 0;
                foreach (var result in _results)
                    total += result.Score;
                return total;
            }
        }

        public int BestTotal
        {
            get
            {
                var total = 0;
                foreach (var result in _results)
                    total += result.BestScore;
                return total;
            }
        }

        public void Record(Scenario scenario, DialogueOption chosen)
        {
            _results.Add(new ScenarioResult(scenario, chosen));
        }

        public void Clear()
        {
            _results.Clear();
        }
    }
}
