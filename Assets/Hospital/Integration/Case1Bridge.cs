using System.Linq;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// Taak 1 (kamer 1A): de pillenstrip van Case1_Props volgt het gesprek. Bij de start van het scenario ligt hij op
    /// het nachtkastje; na de keuze met de hoogste score gaat hij naar het dienblad (genomen), na een andere keuze naar
    /// de vensterbank (weggeschoven).
    /// </summary>
    public class Case1Bridge : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;
        [SerializeField] private ItemSpots _spots;
        [SerializeField] private string _startSpot = "Spot_Nightstand";
        [SerializeField] private string _takenSpot = "Spot_Tray";
        [SerializeField] private string _pushedAwaySpot = "Spot_Windowsill";

        public void Configure(ScenarioPresenter presenter, ItemSpots spots)
        {
            _presenter = presenter;
            _spots = spots;
        }

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.OptionChosen += OnOptionChosen;
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.OptionChosen -= OnOptionChosen;
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            if (_spots != null)
                _spots.MoveTo(_startSpot);
        }

        private void OnOptionChosen(Scenario scenario, ProCommVR.Scenarios.DialogueOption option)
        {
            if (_spots == null || option == null)
                return;
            var best = scenario.Options.Max(o => o.Score);
            _spots.MoveTo(option.Score >= best ? _takenSpot : _pushedAwaySpot);
        }
    }
}
