using SharpDX.DirectInput;
using SimConnect.NET;
using System.Diagnostics;

namespace ButtonBaby
{
    public partial class Form1 : Form
    {
        private Joystick? _joystick;
        private DirectInput? _directInput;
        private SimConnectClient? _simconnect;

        private volatile bool _isRunning = false;

        // Virtueller Collective-Wert
        // 0  = 0 %
        // 100 = 100 %
        private int _virtualCollective = 0;

        // Haltezeiten der beiden Buttons
        private int _button0HeldTicks = 0;
        private int _button2HeldTicks = 0;

        // Dein funktionierender Collective-Input-Event
        private const ulong CollectiveEventHash =
            9384294382955118306UL;

        public Form1()
        {
            InitializeComponent();

            textBox1.Text =
                "BUTTONBABY\r\n" +
                "============================\r\n\r\n" +
                "Bereit.\r\n\r\n" +
                "Button 0 = +1 %\r\n" +
                "Button 2 = -1 %";
        }

        // ============================================================
        // BUTTON 1 DES PROGRAMMS:
        // START
        // ============================================================

        private async void button1_Click(
            object sender,
            EventArgs e)
        {
            if (_isRunning)
                return;

            button1.Enabled = false;

            try
            {
                // ----------------------------------------------------
                // 1. JOYSTICK SUCHEN
                // ----------------------------------------------------

                textBox1.Text =
                    "BUTTONBABY\r\n" +
                    "============================\r\n\r\n" +
                    "1. Joystick wird gesucht...";

                _directInput =
                    new DirectInput();

                Guid joystickGuid =
                    Guid.Empty;

                var devices =
                    _directInput.GetDevices(
                        DeviceType.Joystick,
                        DeviceEnumerationFlags.AllDevices);

                if (devices.Count > 0)
                {
                    joystickGuid =
                        devices[0].InstanceGuid;
                }
                else
                {
                    var gamepads =
                        _directInput.GetDevices(
                            DeviceType.Gamepad,
                            DeviceEnumerationFlags.AllDevices);

                    if (gamepads.Count > 0)
                    {
                        joystickGuid =
                            gamepads[0].InstanceGuid;
                    }
                }

                if (joystickGuid == Guid.Empty)
                {
                    textBox1.AppendText(
                        "\r\n\r\n" +
                        "Kein Joystick oder Gamepad gefunden!");

                    button1.Enabled = true;
                    return;
                }

                // ----------------------------------------------------
                // JOYSTICK ÖFFNEN
                // ----------------------------------------------------

                _joystick =
                    new Joystick(
                        _directInput,
                        joystickGuid);

                _joystick.SetCooperativeLevel(
                    this.Handle,
                    CooperativeLevel.Background |
                    CooperativeLevel.NonExclusive);

                _joystick.Acquire();

                textBox1.AppendText(
                    "\r\nJoystick verbunden:");

                textBox1.AppendText(
                    "\r\n" +
                    _joystick.Information.InstanceName);

                // ----------------------------------------------------
                // 2. SIMCONNECT
                // ----------------------------------------------------

                textBox1.AppendText(
                    "\r\n\r\n2. Verbindung mit MSFS 2024...");

                _simconnect =
                    new SimConnectClient();

                textBox1.AppendText(
                    "\r\nSimConnectClient erstellt.");

                await _simconnect.ConnectAsync();

                if (!_simconnect.IsConnected)
                {
                    throw new Exception(
                        "MSFS 2024 konnte nicht verbunden werden.");
                }

                textBox1.AppendText(
                    "\r\nMSFS 2024 verbunden.");

                // ----------------------------------------------------
                // START
                // ----------------------------------------------------

                _virtualCollective = 0;

                _button0HeldTicks = 0;
                _button2HeldTicks = 0;

                _isRunning = true;

                textBox1.AppendText(
                    "\r\n\r\n============================");

                textBox1.AppendText(
                    "\r\nBUTTONBABY IST BEREIT!");

                textBox1.AppendText(
                    "\r\n============================");

                textBox1.AppendText(
                    "\r\n\r\nButton 0 = Collective +1 %");

                textBox1.AppendText(
                    "\r\nButton 2 = Collective -1 %");

                textBox1.AppendText(
                    "\r\n\r\nKollektiv: 0 %");

                // ----------------------------------------------------
                // POLLING STARTEN
                // ----------------------------------------------------

                _ = Task.Run(StartPolling);
            }
            catch (Exception ex)
            {
                _isRunning = false;

                textBox1.AppendText(
                    "\r\n\r\nFEHLER:");

                textBox1.AppendText(
                    "\r\n" +
                    ex.Message);

                Cleanup();

                button1.Enabled = true;
            }
        }

        // ============================================================
        // JOYSTICK ABFRAGEN
        // ============================================================

        private void StartPolling()
        {
            if (_joystick == null)
                return;

            while (_isRunning)
            {
                try
                {
                    _joystick.Poll();

                    var state =
                        _joystick.GetCurrentState();

                    // =================================================
                    // BUTTON 0
                    // COLLECTIVE ERHÖHEN
                    // =================================================

                    if (state.Buttons.Length > 0 &&
                        state.Buttons[0])
                    {
                        _button0HeldTicks++;

                        // Beim ersten Druck sofort reagieren.
                        // Danach bei längerem Halten wiederholen.
                        if (_button0HeldTicks == 1 ||
                            _button0HeldTicks > 12)
                        {
                            if (_virtualCollective < 100)
                            {
                                _virtualCollective++;

                                int collective =
                                    _virtualCollective;

                                UpdateDisplay(
                                    collective,
                                    "ERHÖHEN");

                                _ = SendCollectiveAsync(
                                    collective);
                            }
                        }
                    }
                    else
                    {
                        _button0HeldTicks = 0;
                    }

                    // =================================================
                    // BUTTON 2
                    // COLLECTIVE VERRINGERN
                    // =================================================

                    if (state.Buttons.Length > 2 &&
                        state.Buttons[2])
                    {
                        _button2HeldTicks++;

                        // Beim ersten Druck sofort reagieren.
                        // Danach bei längerem Halten wiederholen.
                        if (_button2HeldTicks == 1 ||
                            _button2HeldTicks > 12)
                        {
                            if (_virtualCollective > 0)
                            {
                                _virtualCollective--;

                                int collective =
                                    _virtualCollective;

                                UpdateDisplay(
                                    collective,
                                    "VERRINGERN");

                                _ = SendCollectiveAsync(
                                    collective);
                            }
                        }
                    }
                    else
                    {
                        _button2HeldTicks = 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "Joystick-Fehler: " +
                        ex.Message);

                    UpdateError(
                        "Joystick-Fehler: " +
                        ex.Message);

                    _isRunning = false;
                }

                Thread.Sleep(30);
            }
        }

        // ============================================================
        // COLLECTIVE AN MSFS SENDEN
        // ============================================================

        private async Task SendCollectiveAsync(
            int collective)
        {
            try
            {
                if (_simconnect == null ||
                    !_simconnect.IsConnected)
                {
                    return;
                }

                // Prozent in normierten Wert umrechnen.
                //
                // 0 %   = 0.00
                // 1 %   = 0.01
                // 50 %  = 0.50
                // 100 % = 1.00

                double simValue =
                    collective / 100.0;

                await _simconnect
                    .InputEvents
                    .SetInputEventAsync(
                        CollectiveEventHash,
                        simValue,
                        CancellationToken.None);

                Debug.WriteLine(
                    $"Kollektiv gesendet: " +
                    $"{collective}% -> {simValue:0.00}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Übertragungsfehler: " +
                    ex.Message);

                UpdateError(
                    "Übertragungsfehler: " +
                    ex.Message);
            }
        }

        // ============================================================
        // TEXTBOX1 AKTUALISIEREN
        // ============================================================

        private void UpdateDisplay(
            int collective,
            string direction)
        {
            if (IsDisposed ||
                !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() =>
                {
                    textBox1.Text =
                        "BUTTONBABY\r\n" +
                        "============================\r\n\r\n" +

                        "Joystick:\r\n" +
                        (_joystick?.Information.InstanceName
                         ?? "nicht verbunden") +

                        "\r\n\r\n" +

                        "MSFS 2024: " +
                        ((_simconnect != null &&
                          _simconnect.IsConnected)
                            ? "VERBUNDEN"
                            : "NICHT VERBUNDEN") +

                        "\r\n\r\n" +

                        "Button 0 = +1 %" +
                        "\r\nButton 2 = -1 %" +

                        "\r\n\r\n" +

                        "Aktion: " +
                        direction +

                        "\r\n\r\n" +

                        "Kollektiv: " +
                        collective +
                        "%" +

                        "\r\n\r\n" +

                        "Gesendet: " +
                        collective +
                        "% -> " +
                        (collective / 100.0)
                            .ToString("0.00");
                }));
            }
            catch
            {
                // Formular wird eventuell gerade geschlossen.
            }
        }

        // ============================================================
        // FEHLER AUSGEBEN
        // ============================================================

        private void UpdateError(
            string message)
        {
            if (IsDisposed ||
                !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() =>
                {
                    textBox1.AppendText(
                        "\r\n\r\nFEHLER: " +
                        message);
                }));
            }
            catch
            {
            }
        }

        // ============================================================
        // AUFRÄUMEN
        // ============================================================

        private void Cleanup()
        {
            _isRunning = false;

            try
            {
                _joystick?.Unacquire();
                _joystick?.Dispose();
                _joystick = null;

                _directInput?.Dispose();
                _directInput = null;

                if (_simconnect != null)
                {
                    _simconnect
                        .DisconnectAsync()
                        .GetAwaiter()
                        .GetResult();

                    _simconnect.Dispose();
                    _simconnect = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Cleanup-Fehler: " +
                    ex.Message);
            }
        }

        // ============================================================
        // FORM SCHLIESSEN
        // ============================================================

        protected override void OnFormClosing(
            FormClosingEventArgs e)
        {
            Cleanup();

            base.OnFormClosing(e);
        }
    }
}
