using System;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Plays a list of scenarios one after the other through a <see cref="ScenarioPresenter"/>, records
    /// every choice in a <see cref="SessionScore"/> and shows the <see cref="ResultsScreen"/> at the end.
    /// </summary>
    public class ScenarioSequence : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;
        [SerializeField] private ResultsScreen _resultsScreen;

        [Tooltip("Played in this order. Add a scenario by dragging its asset into this list.")]
        [SerializeField] private Scenario[] _scenarios = Array.Empty<Scenario>();

        [SerializeField] private bool _playOnStart = true;

        [Header("Labels")]
        [SerializeField] private string _nextLabel = "Next scenario";
        [SerializeField] private string _resultsLabel = "See results";

        private readonly SessionScore _score = new SessionScore();
        private int _index = -1;

        public SessionScore Score => _score;

        public event Action Finished;

        private void OnEnable()
        {
            _presenter.OptionChosen += OnOptionChosen;
            _presenter.Continued += OnContinued;
            if (_resultsScreen != null)
                _resultsScreen.RestartRequested += Restart;
        }

        private void OnDisable()
        {
            _presenter.OptionChosen -= OnOptionChosen;
            _presenter.Continued -= OnContinued;
            if (_resultsScreen != null)
                _resultsScreen.RestartRequested -= Restart;
        }

        private void Start()
        {
            if (_resultsScreen != null)
                _resultsScreen.Hide();
            if (_playOnStart)
                Restart();
        }

        public void Restart()
        {
            _score.Clear();
            _index = -1;
            if (_resultsScreen != null)
                _resultsScreen.Hide();
            PlayNext();
        }

        private void PlayNext()
        {
            _index++;
            while (_index < _scenarios.Length && _scenarios[_index] == null)
                _index++;

            if (_index >= _scenarios.Length)
            {
                ShowResults();
                return;
            }

            _presenter.ContinueLabel = IsLastScenario(_index) ? _resultsLabel : _nextLabel;
            _presenter.Play(_scenarios[_index]);
        }

        private bool IsLastScenario(int index)
        {
            for (var i = index + 1; i < _scenarios.Length; i++)
            {
                if (_scenarios[i] != null)
                    return false;
            }
            return true;
        }

        private void ShowResults()
        {
            _presenter.Clear();
            if (_resultsScreen != null)
                _resultsScreen.Show(_score);
            else
                Debug.Log($"[ProCommVR] Session finished: {_score.Total} / {_score.BestTotal}. No results screen assigned.", this);
            Finished?.Invoke();
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            _score.Record(scenario, option);
        }

        private void OnContinued()
        {
            PlayNext();
        }
    }
}
