using System;
using System.ComponentModel;

namespace RestaurantPosWpf
{
    public sealed class UiScaleState : INotifyPropertyChanged
    {
        private double _fontScale = 1.0;

        public double FontScale
        {
            get { return _fontScale; }
            set
            {
                if (Math.Abs(_fontScale - value) < 0.0001)
                    return;

                _fontScale = value;

                var handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs("FontScale"));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private double _footerAlignHeight = 0;

        public double FooterAlignHeight
        {
            get { return _footerAlignHeight; }
            set
            {
                if (Math.Abs(_footerAlignHeight - value) < 0.0001)
                    return;

                _footerAlignHeight = value;

                var handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs("FooterAlignHeight"));
            }
        }
    }
}
