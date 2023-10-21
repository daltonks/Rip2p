using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Button = UnityEngine.UI.Button;

namespace Rip2p
{
    public class NetworkView : MonoBehaviour
    {
        [Header("Sub-views")]
        [SerializeField] private GameObject _sessionNotInProgressView;
        [SerializeField] private GameObject _sessionInProgressView;
        [Header("Not in session")]
        [SerializeField] private Button _startServerButton;
        [SerializeField] private TMP_InputField _serverAddressInputField;
        [SerializeField] private TMP_InputField _serverPortInputField;
        [SerializeField] private Button _joinServerButton;
        [SerializeField] private TMP_Text _connectionErrorText;
        [Header("In session")]
        [SerializeField] private Button _leaveServerButton;

        private void Awake()
        {
            ConnectionError = null;
        }

        private void OnEnable()
        {
            UpdateSubviews();
            
            NetworkService.Instance.SessionChanged += OnSessionChanged;
            _startServerButton.onClick.AddListener(OnStartServerButtonClicked);
            _joinServerButton.onClick.AddListener(OnJoinServerButtonClicked);
            _leaveServerButton.onClick.AddListener(OnLeaveServerButtonClicked);
        }

        private void OnDisable()
        {
            NetworkService.Instance.SessionChanged -= OnSessionChanged;
            _startServerButton.onClick.RemoveListener(OnStartServerButtonClicked);
            _joinServerButton.onClick.RemoveListener(OnJoinServerButtonClicked);
            _leaveServerButton.onClick.RemoveListener(OnLeaveServerButtonClicked);
        }
        
        private bool _connecting;
        private bool Connecting
        {
            get => _connecting;
            set
            {
                _connecting = value;
                if (value)
                {
                    _startServerButton.interactable = false;
                    _serverAddressInputField.interactable = false;
                    _serverPortInputField.interactable = false;
                    _joinServerButton.interactable = false;
                }
                else
                {
                    _startServerButton.interactable = true;
                    _serverAddressInputField.interactable = true;
                    _serverPortInputField.interactable = true;
                    _joinServerButton.interactable = true;
                }

                UpdateSubviews();
            }
        }

        private string _connectionError;
        public string ConnectionError
        {
            get => _connectionError;
            set
            {
                _connectionError = value;

                if (string.IsNullOrWhiteSpace(value))
                {
                    _connectionErrorText.gameObject.SetActive(false);
                }
                else
                {
                    _connectionErrorText.text = value;
                    _connectionErrorText.gameObject.SetActive(true);
                }
            }
        }

        private void OnSessionChanged(NetworkSession session)
        {
            UpdateSubviews();
        }

        private void UpdateSubviews()
        {
            var showSessionNotInProgress = Connecting || NetworkService.Instance.Session == null;
            _sessionNotInProgressView.SetActive(showSessionNotInProgress);
            _sessionInProgressView.SetActive(!showSessionNotInProgress);
        }

        private void OnStartServerButtonClicked()
        {
            _ = StartSessionAsync(null, 0);
        }
        
        private void OnJoinServerButtonClicked()
        {
            ushort.TryParse(_serverPortInputField.text, out var serverPort);
            _ = StartSessionAsync(_serverAddressInputField.text, serverPort);
        }

        private async Task StartSessionAsync(string address, ushort port)
        {
            ConnectionError = null;
            Connecting = true;
            
            var result = await NetworkService.Instance.TryStartSessionAsync(
                address,
                port);
            
            ConnectionError = result.message;
            Connecting = false;
        }
        
        private void OnLeaveServerButtonClicked()
        {
            _ = NetworkService.Instance.StopSessionAsync(allowSceneChange: true);
        }
    }
}