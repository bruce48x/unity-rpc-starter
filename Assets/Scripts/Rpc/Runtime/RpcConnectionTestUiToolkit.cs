using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Rpc.Runtime
{
    public sealed class RpcConnectionTestUiToolkit : MonoBehaviour
    {
        private const int DefaultTcpPort = 20000;
        private const int DefaultWsPort = 20001;
        private const int DefaultKcpPort = 20002;

        public RpcConnectionTester? Tester;

        [Header("UI References (optional)")] [SerializeField]
        private Canvas? _canvas;

        [SerializeField] private InputField? _hostField;
        [SerializeField] private InputField? _portField;
        [SerializeField] private InputField? _wsUrlField;
        [SerializeField] private InputField? _accountField;
        [SerializeField] private InputField? _passwordField;
        [SerializeField] private Button? _tcpButton;
        [SerializeField] private Button? _wsButton;
        [SerializeField] private Button? _kcpButton;
        [SerializeField] private Button? _connectButton;
        [SerializeField] private Text? _statusText;

        private Font? _font;

        private void Awake()
        {
            if (Tester is null)
                Tester = GetComponent<RpcConnectionTester>();

            EnsureEventSystem();
            EnsureUi();
            BindUi();
            SyncFromTester();

            if (Tester is not null)
                Tester.StatusChanged += OnStatusChanged;
        }

        private void OnDestroy()
        {
            if (Tester is not null)
                Tester.StatusChanged -= OnStatusChanged;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() is not null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(null, false);
        }

        private void EnsureUi()
        {
            var hasAll =
                _canvas is not null &&
                _hostField is not null &&
                _portField is not null &&
                _wsUrlField is not null &&
                _accountField is not null &&
                _passwordField is not null &&
                _tcpButton is not null &&
                _wsButton is not null &&
                _connectButton is not null;

            if (hasAll)
                return;

            BuildRuntimeUi();
        }

        private void BindUi()
        {
            if (_tcpButton is not null)
                _tcpButton.onClick.AddListener(() => SetKind(TransportKind.Tcp));
            if (_wsButton is not null)
                _wsButton.onClick.AddListener(() => SetKind(TransportKind.WebSocket));
            if (_kcpButton is not null)
                _kcpButton.onClick.AddListener(() => SetKind(TransportKind.Kcp));
            if (_connectButton is not null)
                _connectButton.onClick.AddListener(Connect);
        }

        private void SyncFromTester()
        {
            if (Tester is null)
                return;

            if (_hostField is not null) _hostField.text = Tester.Host;
            if (_portField is not null) _portField.text = Tester.Port.ToString();
            if (_wsUrlField is not null) _wsUrlField.text = Tester.WsUrl;
            if (_accountField is not null) _accountField.text = Tester.Account;
            if (_passwordField is not null) _passwordField.text = Tester.Password;
        }

        private void SetKind(TransportKind kind)
        {
            if (Tester is null)
                return;

            Tester.Kind = kind;
            ApplyKindDefaults(kind);
            SetStatus($"Transport set: {kind}");
        }

        private void Connect()
        {
            if (Tester is null)
            {
                Debug.LogError("RpcConnectionTester not found.");
                return;
            }

            var host = _hostField?.text ?? "127.0.0.1";
            var portText = _portField?.text ?? "20000";
            var wsUrl = _wsUrlField?.text ?? "ws://127.0.0.1:20001/rpc";
            var account = _accountField?.text ?? "a";
            var password = _passwordField?.text ?? "b";

            if (!int.TryParse(portText, out var port))
            {
                Debug.LogError($"Invalid port: {portText}");
                SetStatus($"Invalid port: {portText}");
                return;
            }

            Tester.Host = host;
            Tester.Port = port;
            Tester.WsUrl = wsUrl;
            Tester.Account = account;
            Tester.Password = password;

            Tester.ConnectFromUi();
            SetStatus("Connecting...");
        }

        private void SetStatus(string message)
        {
            if (_statusText is not null)
                _statusText.text = message;
        }

        private void OnStatusChanged(RpcConnectionTester.ConnectionState state, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = state.ToString();

            SetStatus(message);
        }

        private void ApplyKindDefaults(TransportKind kind)
        {
            if (_portField is null || _wsUrlField is null)
                return;

            switch (kind)
            {
                case TransportKind.Tcp:
                    _portField.text = DefaultTcpPort.ToString();
                    break;
                case TransportKind.WebSocket:
                    _portField.text = DefaultWsPort.ToString();
                    _wsUrlField.text = $"ws://127.0.0.1:{DefaultWsPort}/rpc";
                    break;
                case TransportKind.Kcp:
                    _portField.text = DefaultKcpPort.ToString();
                    break;
            }
        }

        private void BuildRuntimeUi()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("RpcConnectionTestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            var panel = CreatePanel(canvasGo.transform);

            CreateTitle(panel, "RPC Connection Test");

            _hostField = CreateLabeledInput(panel, "Host", "127.0.0.1", false);
            _portField = CreateLabeledInput(panel, "Port", "20000", false);
            _wsUrlField = CreateLabeledInput(panel, "WS Url", "ws://127.0.0.1:20001/rpc", false);
            _accountField = CreateLabeledInput(panel, "Account", "a", false);
            _passwordField = CreateLabeledInput(panel, "Password", "b", true);

            var buttonRow = CreateRow(panel);
            _tcpButton = CreateButton(buttonRow, "TCP");
            _wsButton = CreateButton(buttonRow, "WebSocket");
            _kcpButton = CreateButton(buttonRow, "KCP");
            _connectButton = CreateButton(buttonRow, "Connect");

            _statusText = CreateStatus(panel, "Idle");
        }

        private RectTransform CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("Panel",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(420f, 0f);

            var image = panelGo.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.9f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        private void CreateTitle(Transform parent, string text)
        {
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(parent, false);

            var title = titleGo.GetComponent<Text>();
            title.text = text;
            title.font = _font;
            title.fontSize = 20;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(0.1f, 0.1f, 0.1f);

            var layout = titleGo.GetComponent<LayoutElement>();
            layout.preferredHeight = 30f;
        }

        private Transform CreateRow(Transform parent)
        {
            var rowGo = new GameObject("Row",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var element = rowGo.GetComponent<LayoutElement>();
            element.preferredHeight = 32f;

            return rowGo.transform;
        }

        private InputField CreateLabeledInput(Transform parent, string label, string placeholder, bool isPassword)
        {
            var row = CreateRow(parent);

            var labelGo = new GameObject($"{label}Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            labelGo.transform.SetParent(row, false);
            var labelText = labelGo.GetComponent<Text>();
            labelText.text = label;
            labelText.font = _font;
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.1f, 0.1f, 0.1f);

            var labelLayout = labelGo.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 90f;
            labelLayout.minWidth = 90f;

            var inputGo = new GameObject($"{label}Input", typeof(RectTransform), typeof(Image), typeof(InputField),
                typeof(LayoutElement));
            inputGo.transform.SetParent(row, false);

            var inputImage = inputGo.GetComponent<Image>();
            inputImage.color = Color.white;

            var inputField = inputGo.GetComponent<InputField>();
            inputField.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;
            inputField.lineType = InputField.LineType.SingleLine;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(inputGo.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = _font;
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(inputGo.transform, false);
            var placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.font = _font;
            placeholderText.fontSize = 14;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.text = placeholder;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.color = new Color(0f, 0f, 0f, 0.4f);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);

            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 6f);
            placeholderRect.offsetMax = new Vector2(-8f, -6f);

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;

            var inputLayout = inputGo.GetComponent<LayoutElement>();
            inputLayout.preferredHeight = 28f;
            inputLayout.minWidth = 200f;

            return inputField;
        }

        private Button CreateButton(Transform parent, string label)
        {
            var buttonGo = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            buttonGo.transform.SetParent(parent, false);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.2f, 0.55f, 0.9f);

            var button = buttonGo.GetComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(buttonGo.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var layout = buttonGo.GetComponent<LayoutElement>();
            layout.preferredHeight = 30f;

            return button;
        }

        private Text CreateStatus(Transform parent, string text)
        {
            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            statusGo.transform.SetParent(parent, false);

            var status = statusGo.GetComponent<Text>();
            status.text = text;
            status.font = _font;
            status.fontSize = 12;
            status.alignment = TextAnchor.MiddleLeft;
            status.color = new Color(0.2f, 0.2f, 0.2f);

            var layout = statusGo.GetComponent<LayoutElement>();
            layout.preferredHeight = 20f;

            return status;
        }
    }
}