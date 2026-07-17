using eSureHi.ViewModels.Admin;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class BenefitFormDialog : Window
    {
        private readonly BenefitFormViewModel _vm;
        private bool _isFormattingAmount;

        public BenefitFormDialog()
        {
            InitializeComponent();
            _vm = new BenefitFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public BenefitFormDialog(int benefitId) : this()
        {
            _ = _vm.InitEditAsync(benefitId);
        }

        public void SetSaveCallback(Action onSave)
            => _vm.OnSaveSuccess = onSave;

        private void MaxBenefitTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text == "0.00")
            {
                textBox.SelectAll();
            }
        }

        private void MaxBenefitTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(ch => char.IsDigit(ch) || ch == '.');
        }

        private void MaxBenefitTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void MaxBenefitTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingAmount || sender is not TextBox textBox)
            {
                return;
            }

            _isFormattingAmount = true;
            try
            {
                var originalText = textBox.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(originalText))
                {
                    _vm.MaxBenefitText = string.Empty;
                    return;
                }

                var caretIndex = textBox.CaretIndex;
                var digitsBeforeCaret = CountEditableCharacters(originalText, caretIndex);
                var sanitized = SanitizeAmount(originalText);

                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    textBox.Text = string.Empty;
                    _vm.MaxBenefitText = string.Empty;
                    return;
                }

                if (sanitized == ".")
                {
                    sanitized = "0.";
                }

                if (!decimal.TryParse(sanitized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                {
                    parsed = 0m;
                }

                var formatted = parsed.ToString("N2", CultureInfo.InvariantCulture);
                if (!string.Equals(textBox.Text, formatted, StringComparison.Ordinal))
                {
                    textBox.Text = formatted;
                }

                _vm.MaxBenefitText = formatted;
                textBox.CaretIndex = FindCaretIndex(formatted, digitsBeforeCaret);
            }
            finally
            {
                _isFormattingAmount = false;
            }
        }

        private static string SanitizeAmount(string text)
        {
            var chars = text.Where(ch => char.IsDigit(ch) || ch == '.').ToArray();
            var cleaned = new string(chars);
            var decimalIndex = cleaned.IndexOf('.');

            if (decimalIndex >= 0)
            {
                var whole = cleaned[..decimalIndex];
                var fraction = cleaned[(decimalIndex + 1)..].Replace(".", string.Empty);
                return fraction.Length > 0 ? $"{whole}.{fraction}" : whole;
            }

            return cleaned;
        }

        private static int CountEditableCharacters(string text, int caretIndex)
        {
            var length = Math.Max(0, Math.Min(caretIndex, text.Length));
            return text[..length].Count(ch => char.IsDigit(ch) || ch == '.');
        }

        private static int FindCaretIndex(string formattedText, int editableCharacters)
        {
            if (editableCharacters <= 0)
            {
                return 0;
            }

            var seen = 0;
            for (var i = 0; i < formattedText.Length; i++)
            {
                if (char.IsDigit(formattedText[i]) || formattedText[i] == '.')
                {
                    seen++;
                    if (seen >= editableCharacters)
                    {
                        return i + 1;
                    }
                }
            }

            return formattedText.Length;
        }
    }
}
