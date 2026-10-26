using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// Taak 5 (gedeelde kamer 1C): de buurman luistert mee. Jalils ScenarioStarted komt voor alle kamers tegelijk bij
    /// het begin van de shift, dus die zet het oor enkel klaar; het groeit (EarGrow, 25 s) zodra de speler de kamer
    /// nadert. Kiest de speler het privacygordijn (LocationChosen met id "curtain"), dan krimpt het oor terug
    /// (ResetEar) en gaat de krant weer omhoog (Jalils twee krant-objecten: lezend aan, neergelegd uit).
    /// EarGrow wordt pas in Play mode aan de buurman toegevoegd; Jalils objecten blijven in de scene ongewijzigd.
    /// </summary>
    public class EavesdropBridge : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;
        [SerializeField] private Animator _neighbour;
        [SerializeField] private GameObject _paperReading;
        [SerializeField] private GameObject _paperLowered;
        [SerializeField] private string _scenarioId = "aggressive-patient-privacy";
        [SerializeField] private string _curtainLocationId = "curtain";
        [SerializeField] private float _growSeconds = 25f;
        [Tooltip("Het oor begint te groeien als de speler binnen deze horizontale afstand van de buurman komt (zelfde verdieping).")]
        [SerializeField] private float _triggerDistance = 4.5f;

        private EarGrow _ear;
        private bool _armed;
        private bool _growing;

        public void Configure(ScenarioPresenter presenter, Animator neighbour, GameObject paperReading, GameObject paperLowered)
        {
            _presenter = presenter;
            _neighbour = neighbour;
            _paperReading = paperReading;
            _paperLowered = paperLowered;
        }

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.LocationChosen += OnLocationChosen;
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.LocationChosen -= OnLocationChosen;
        }

        private void Start()
        {
            if (_neighbour != null && !_neighbour.TryGetComponent(out _ear))
                _ear = _neighbour.gameObject.AddComponent<EarGrow>();
        }

        private void Update()
        {
            if (!_armed || _growing || _ear == null)
                return;
            var player = Camera.main;
            if (player == null)
                return;
            var offset = player.transform.position - _neighbour.transform.position;
            if (Mathf.Abs(offset.y) < 2.5f && new Vector2(offset.x, offset.z).magnitude < _triggerDistance)
            {
                _ear.Grow(_growSeconds);
                _growing = true;
            }
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            if (scenario != null && scenario.Id == _scenarioId)
                _armed = true;
        }

        private void OnLocationChosen(Scenario scenario, ConversationLocation location)
        {
            if (scenario == null || scenario.Id != _scenarioId || location == null || location.Id != _curtainLocationId)
                return;
            _armed = false;
            if (_ear != null)
                _ear.ResetEar();
            if (_paperReading != null)
                _paperReading.SetActive(true);
            if (_paperLowered != null)
                _paperLowered.SetActive(false);
        }
    }
}
