using Microsoft.Extensions.Options;
using Microsoft.Win32;
using OpenRGB.NET;
using XColor = System.Drawing.Color;

namespace enfasis_color
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private OpenRgbClient? _client;
        private Device? _device;
        private Zone? _zone;
        private const int fps = 5;
        private readonly string userSid;
        private const int retryDelayMs = 3000; // Tiempo entre reintentos (3 segundos)

        public Worker(ILogger<Worker> logger, SID? sid)
        {
            _logger = logger;
            userSid = sid?.value ?? "";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || !_client.Connected) TryConnect();

                    if (_client != null && _client.Connected) {
                        //if (_device == null) {

                            RegistryKey? accentColorReg = Registry.Users.OpenSubKey(@$"{userSid}\Software\Asra\AccentColor");
                            if (accentColorReg == null) { 
                                _logger.LogWarning("No se encontró la clave del registro de AccentColor.");
                                await Task.Delay(retryDelayMs, stoppingToken);
                                continue;
                            }

                            string? deviceName = accentColorReg.GetValue("DeviceName") as string;
                            string? deviceZone = accentColorReg.GetValue("deviceZone") as string;
                            if (string.IsNullOrEmpty(deviceName) || string.IsNullOrEmpty(deviceZone))
                            {
                                _logger.LogWarning("No se encontró el nombre del dispositivo en el registro.");
                                await Task.Delay(retryDelayMs, stoppingToken);
                                continue;
                            }

                            

                            Device[] devices = _client.GetAllControllerData();
                            _device = devices.FirstOrDefault(d => d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

                            if (_device == null)
                            {
                                _logger.LogWarning("No se encontró ningún dispositivo RGB.");
                                await Task.Delay(retryDelayMs, stoppingToken);
                                continue;
                            }


                            _zone = _device.Zones.FirstOrDefault(z => z.Name.Equals(deviceZone, StringComparison.OrdinalIgnoreCase));
                            if(_zone == null)
                            {
                                _logger.LogWarning("No se encontró ninguna zona");
                                await Task.Delay(retryDelayMs, stoppingToken);
                                continue;
                            }

                            //_logger.LogInformation("Usando dispositivo: {Name}. Zona: {Zone}", _device.Name, _zone.Name);
                        //}

                        // Actualizamos color
                        XColor accent = GetAccentColor();
                        Color rgbColor = new(accent.R, accent.G, accent.B);
                        Color[] colors = [.. Enumerable.Repeat(rgbColor, int.Parse(_zone.LedCount.ToString()))];
                        _client.UpdateZoneLeds(_device.Index, _zone.Index, colors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la ejecución del loop. Se intentará reconectar.");
                    DisposeClient();
                }

                try
                {
                    await Task.Delay(1000 / fps, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Ignorado: cancelación esperada
                }
            }

            DisposeClient();
            _logger.LogInformation("Worker detenido.");
        }

        private void TryConnect()
        {
            try
            {
                DisposeClient(); // Limpiar conexiones previas
                _client = new OpenRgbClient(name: "AccentColorSync", autoConnect: false);
                _client.Connect();
                _logger.LogInformation("Conectado a OpenRGB.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo conectar a OpenRGB. Se reintentará.");
                DisposeClient();
            }
        }

        private void DisposeClient()
        {
            if (_client != null)
            {
                try
                {
                    _client.Dispose();
                }
                catch { }
                _client = null;
                _device = null;
            }
        }

        private XColor GetAccentColor()
        {

            using var key = Registry.Users.OpenSubKey($@"{userSid}\Software\Microsoft\Windows\DWM");
            if (key != null)
            {
                object? value = key.GetValue("AccentColor");
                if (value is int colorValue)
                {
                    byte a = (byte)((colorValue >> 24) & 0xFF);
                    byte b = (byte)((colorValue >> 16) & 0xFF);
                    byte g = (byte)((colorValue >> 8) & 0xFF);
                    byte r = (byte)(colorValue & 0xFF);
                    return XColor.FromArgb(a, r, g, b);
                } else return XColor.Empty;
            } else return XColor.Empty;
        }

       
    }
}