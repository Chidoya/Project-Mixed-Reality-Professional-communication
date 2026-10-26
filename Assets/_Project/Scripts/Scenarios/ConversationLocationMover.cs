using System;
using ProCommVR.Navigation;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Takes the NPC to the place the player chose to talk (<see cref="ScenarioPresenter.LocationChosen"/>), e.g.
    /// walks the angry daughter from the corridor into the consultation room. One <see cref="PathMover"/> per place,
    /// on the NPC's root (so the NPC bubble, pose targets and sound move along); a place without a mover keeps the
    /// NPC where it is. The options only appear once the NPC has arrived. When a scenario starts, the NPC goes back
    /// to its start.
    /// </summary>
    public class ConversationLocationMover : MonoBehaviour
    {
        [Serializable]
        private class Place
        {
            [Tooltip("Id of the place in the scenario (Locations), e.g. \"consultation-room\".")]
            public string LocationId;

            [Tooltip("Walks the NPC to this place. Empty: the NPC stays where it is.")]
            public PathMover Path;
        }

        [SerializeField] private ScenarioPresenter _presenter;
        [SerializeField] private Place[] _places = Array.Empty<Place>();

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.LocationChosen += OnLocationChosen;
            foreach (var place in _places)
            {
                if (place?.Path != null)
                    place.Path.Arrived += OnArrived;
            }
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.LocationChosen -= OnLocationChosen;
            foreach (var place in _places)
            {
                if (place?.Path != null)
                    place.Path.Arrived -= OnArrived;
            }
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            _presenter.HoldChoices = false;
            foreach (var place in _places)
            {
                if (place?.Path != null)
                    place.Path.ResetToStart();
            }
        }

        private void OnLocationChosen(Scenario scenario, ConversationLocation location)
        {
            foreach (var place in _places)
            {
                if (place != null && place.LocationId == location.Id)
                {
                    if (place.Path != null)
                    {
                        _presenter.HoldChoices = true;
                        place.Path.Play();
                    }
                    return;
                }
            }
            Debug.LogWarning($"[ProCommVR] No place '{location.Id}' in the scene for scenario '{scenario.Id}'; the NPC stays where it is.", this);
        }

        private void OnArrived(PathMover path)
        {
            _presenter.HoldChoices = false;
        }
    }
}
