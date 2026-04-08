using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RestaurantPosWpf
{
    public enum SpinnerInputMode
    {
        Integer = 0,
        Decimal = 1
    }

    public class SpinnerTextBox : TextBox
    {
        private RepeatButton _upButton;
        private RepeatButton _downButton;

        public static readonly DependencyProperty UpCommandProperty =
            DependencyProperty.Register("UpCommand", typeof(ICommand), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty DownCommandProperty =
            DependencyProperty.Register("DownCommand", typeof(ICommand), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty UpCommandParameterProperty =
            DependencyProperty.Register("UpCommandParameter", typeof(object), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty DownCommandParameterProperty =
            DependencyProperty.Register("DownCommandParameter", typeof(object), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register("Step", typeof(Int32), typeof(SpinnerTextBox), new PropertyMetadata(1));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(Int32?), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(Int32?), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty InputModeProperty =
            DependencyProperty.Register("InputMode", typeof(SpinnerInputMode), typeof(SpinnerTextBox), new PropertyMetadata(SpinnerInputMode.Integer));

        public static readonly DependencyProperty DecimalStepProperty =
            DependencyProperty.Register("DecimalStep", typeof(Decimal), typeof(SpinnerTextBox), new PropertyMetadata(1m));

        public static readonly DependencyProperty DecimalMinimumProperty =
            DependencyProperty.Register("DecimalMinimum", typeof(Decimal?), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public static readonly DependencyProperty DecimalMaximumProperty =
            DependencyProperty.Register("DecimalMaximum", typeof(Decimal?), typeof(SpinnerTextBox), new PropertyMetadata(null));

        public ICommand UpCommand
        {
            get { return (ICommand)GetValue(UpCommandProperty); }
            set { SetValue(UpCommandProperty, value); }
        }

        public ICommand DownCommand
        {
            get { return (ICommand)GetValue(DownCommandProperty); }
            set { SetValue(DownCommandProperty, value); }
        }

        public object UpCommandParameter
        {
            get { return GetValue(UpCommandParameterProperty); }
            set { SetValue(UpCommandParameterProperty, value); }
        }

        public object DownCommandParameter
        {
            get { return GetValue(DownCommandParameterProperty); }
            set { SetValue(DownCommandParameterProperty, value); }
        }

        public Int32 Step
        {
            get { return (Int32)GetValue(StepProperty); }
            set { SetValue(StepProperty, value); }
        }

        public Int32? Minimum
        {
            get { return (Int32?)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public Int32? Maximum
        {
            get { return (Int32?)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public SpinnerInputMode InputMode
        {
            get { return (SpinnerInputMode)GetValue(InputModeProperty); }
            set { SetValue(InputModeProperty, value); }
        }

        public Decimal DecimalStep
        {
            get { return (Decimal)GetValue(DecimalStepProperty); }
            set { SetValue(DecimalStepProperty, value); }
        }

        public Decimal? DecimalMinimum
        {
            get { return (Decimal?)GetValue(DecimalMinimumProperty); }
            set { SetValue(DecimalMinimumProperty, value); }
        }

        public Decimal? DecimalMaximum
        {
            get { return (Decimal?)GetValue(DecimalMaximumProperty); }
            set { SetValue(DecimalMaximumProperty, value); }
        }

        static SpinnerTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SpinnerTextBox),
                new FrameworkPropertyMetadata(typeof(SpinnerTextBox)));
        }

        public SpinnerTextBox()
        {
            System.Windows.DataObject.AddPastingHandler(this, OnPaste);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_upButton != null)
                _upButton.Click -= UpButton_Click;

            if (_downButton != null)
                _downButton.Click -= DownButton_Click;

            _upButton = GetTemplateChild("PART_UpButton") as RepeatButton;
            _downButton = GetTemplateChild("PART_DownButton") as RepeatButton;

            if (_upButton != null)
                _upButton.Click += UpButton_Click;

            if (_downButton != null)
                _downButton.Click += DownButton_Click;
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                base.OnPreviewTextInput(e);
                return;
            }

            string candidate = BuildProposedText(e.Text);

            Boolean isValid = (InputMode == SpinnerInputMode.Decimal)
                ? IsValidDecimalCandidate(candidate)
                : IsValidIntegerCandidate(candidate);

            if (!isValid)
            {
                e.Handled = true;
                return;
            }

            base.OnPreviewTextInput(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            if (InputMode == SpinnerInputMode.Decimal)
                NormalizeDecimalTextOnCommit();
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pastedText = e.DataObject.GetData(System.Windows.DataFormats.Text) as string;
            string candidate = BuildProposedText(pastedText ?? string.Empty);

            Boolean isValid = (InputMode == SpinnerInputMode.Decimal)
                ? IsValidDecimalCandidate(candidate)
                : IsValidIntegerCandidate(candidate);

            if (!isValid)
                e.CancelCommand();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryExecute(UpCommand, UpCommandParameter))
            {
                if (InputMode == SpinnerInputMode.Decimal)
                    SpinDecimal(Decimal.Abs(DecimalStep));
                else
                    SpinInteger(Math.Abs(Step));
            }
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryExecute(DownCommand, DownCommandParameter))
            {
                if (InputMode == SpinnerInputMode.Decimal)
                    SpinDecimal(-Decimal.Abs(DecimalStep));
                else
                    SpinInteger(-Math.Abs(Step));
            }
        }

        private bool TryExecute(ICommand command, object parameter)
        {
            if (command == null)
                return false;

            if (!command.CanExecute(parameter))
                return true;

            command.Execute(parameter);
            return true;
        }

        private void SpinInteger(Int32 delta)
        {
            Int32 current;

            if (!Int32.TryParse((Text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out current))
                current = 0;

            Int32 next = checked(current + delta);

            if (Minimum.HasValue && next < Minimum.Value)
                next = Minimum.Value;

            if (Maximum.HasValue && next > Maximum.Value)
                next = Maximum.Value;

            Text = next.ToString(CultureInfo.InvariantCulture);
            CaretIndex = Text.Length;
            Select(Text.Length, 0);
        }

        private void SpinDecimal(Decimal delta)
        {
            Decimal current;
            string raw = (Text ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(raw) || raw.Equals(".", StringComparison.Ordinal))
                current = 0m;
            else if (!Decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out current))
                current = 0m;

            Decimal next = current + delta;

            if (DecimalMinimum.HasValue && next < DecimalMinimum.Value)
                next = DecimalMinimum.Value;

            if (DecimalMaximum.HasValue && next > DecimalMaximum.Value)
                next = DecimalMaximum.Value;

            Text = next.ToString("0.00", CultureInfo.InvariantCulture);
            CaretIndex = Text.Length;
            Select(Text.Length, 0);
        }

        private string BuildProposedText(string incomingText)
        {
            string currentText = Text ?? string.Empty;
            Int32 start = SelectionStart;
            Int32 length = SelectionLength;

            if (start < 0)
                start = 0;

            if (start > currentText.Length)
                start = currentText.Length;

            if (length < 0)
                length = 0;

            if ((start + length) > currentText.Length)
                length = currentText.Length - start;

            string before = currentText.Substring(0, start);
            string after = currentText.Substring(start + length);

            return before + incomingText + after;
        }

        private bool IsValidIntegerCandidate(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
                return true;

            for (Int32 i = 0; i < candidate.Length; i++)
            {
                if (!Char.IsDigit(candidate[i]))
                    return false;
            }

            return true;
        }

        private bool IsValidDecimalCandidate(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
                return true;

            Int32 dotCount = 0;

            for (Int32 i = 0; i < candidate.Length; i++)
            {
                Char c = candidate[i];

                if (Char.IsDigit(c))
                    continue;

                if (c == '.')
                {
                    dotCount++;
                    if (dotCount > 1)
                        return false;

                    continue;
                }

                return false;
            }

            return true;
        }

        private void NormalizeDecimalTextOnCommit()
        {
            string raw = (Text ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(raw))
            {
                Text = string.Empty;
                return;
            }

            if (raw.Equals(".", StringComparison.Ordinal))
            {
                Text = string.Empty;
                return;
            }

            Decimal value;
            if (!Decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                Text = string.Empty;
                return;
            }

            if (DecimalMinimum.HasValue && value < DecimalMinimum.Value)
                value = DecimalMinimum.Value;

            if (DecimalMaximum.HasValue && value > DecimalMaximum.Value)
                value = DecimalMaximum.Value;

            Text = value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}