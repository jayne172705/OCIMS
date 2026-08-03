using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using MaterialDesignThemes.Wpf;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class QRScannerDialog : UserControl, IDisposable
    {
        private VideoCapture? _capture;
        private Mat? _frame;
        private DispatcherTimer? _timer;
        private QRCodeDetector? _qrDetector;
        private bool _isDisposed;
        private bool _isProcessingFrame;

        public event Action<string>? QRCodeScanned;

        public QRScannerDialog()
        {
            InitializeComponent();
            _qrDetector = new QRCodeDetector();

            this.Loaded += QRScannerDialog_Loaded;
            this.Unloaded += QRScannerDialog_Unloaded;
        }

        private void QRScannerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            StartCamera();
        }

        private void QRScannerDialog_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        private void StartCamera()
        {
            Task.Run(() =>
            {
                try
                {
                    _capture = new VideoCapture(0);
                    if (_capture.IsOpened())
                    {
                        _frame = new Mat();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _timer = new DispatcherTimer();
                            _timer.Interval = TimeSpan.FromMilliseconds(30); // ~30 fps
                            _timer.Tick += Timer_Tick;
                            _timer.Start();
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NoCameraText.Visibility = Visibility.Visible;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Camera scanning is unavailable because a required component failed to load: {ex.Message}",
                            "Camera Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                        DialogHost.CloseDialogCommand.Execute(null, this);
                    });
                }
            });
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isProcessingFrame) return;
            if (_capture != null && _capture.IsOpened() && _frame != null)
            {
                _isProcessingFrame = true;
                try
                {
                    _capture.Read(_frame);
                    if (!_frame.Empty())
                    {
                        CameraPreview.Source = _frame.ToWriteableBitmap();

                        if (_qrDetector != null)
                        {
                            var result = _qrDetector.DetectAndDecode(_frame, out Point2f[] pts);

                            if (!string.IsNullOrWhiteSpace(result))
                            {
                                StopCamera();
                                QRCodeScanned?.Invoke(result);
                                DialogHost.CloseDialogCommand.Execute(null, this);
                            }
                        }
                    }
                }
                finally
                {
                    _isProcessingFrame = false;
                }
            }
        }

        private void SubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            SubmitManualId();
        }

        private void ManualIdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitManualId();
            }
        }

        private void SubmitManualId()
        {
            var id = ManualIdBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                ManualIdBox.Focus();
                return;
            }

            StopCamera();
            QRCodeScanned?.Invoke(id);
            DialogHost.CloseDialogCommand.Execute(null, this);
        }

        private void MirrorCheck_Changed(object sender, RoutedEventArgs e)
        {
            PreviewFlip.ScaleX = MirrorCheck.IsChecked == true ? -1 : 1;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
            DialogHost.CloseDialogCommand.Execute(null, this);
        }

        private void StopCamera()
        {
            _timer?.Stop();
            if (_capture != null)
            {
                _capture.Dispose();
                _capture = null;
            }
            if (_frame != null)
            {
                _frame.Dispose();
                _frame = null;
            }
            if (_qrDetector != null)
            {
                _qrDetector.Dispose();
                _qrDetector = null;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                StopCamera();
                _isDisposed = true;
            }
        }
    }
}
