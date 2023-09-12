using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QtIndicators
{
    /// <summary>
    /// Indicator for coloring bars based on some volume conditions explained at the Tradingriot Bootcamp
    /// https://tradingriot.com/blueprint/tradingriot-bootcamp/
    ///
    /// </summary>
    public class VolumeBars : Indicator
    {
        [InputParameter("Lookback Bars", 12, 1, 100)]
        public int LookbackBars { get; set; } = 30;

        [InputParameter("Color High Volume Bars")]
        public bool ColorHighVolumeBars { get; set; } = true;

        [InputParameter("High Volume Color", 1)]
        public PairColor HighVolumeColor { get; set; } = new PairColor(Color.FromArgb(255, 41, 98, 255), Color.FromArgb(255, 41, 98, 255), "Up", "Down");

        [InputParameter("Color Low Volume Bars", 3)]
        public bool ColorLowVolumeBars { get; set; } = true;

        [InputParameter("Low Volume Color", 4)]
        public PairColor LowVolumeColor { get; set; } = new PairColor(Color.FromArgb(255, 244, 196, 67), Color.FromArgb(255, 244, 196, 67), "Up", "Down");

        [InputParameter("Color Churn Bars", 6)]
        public bool ColorChurnVolumeBars { get; set; } = true;

        [InputParameter("Churn Bar Color", 7)]
        public PairColor ChurnBarColor { get; set; } = new PairColor(Color.FromArgb(255, 233, 30, 99), Color.FromArgb(255, 233, 30, 99), "Up", "Down");
        [InputParameter("Color RVol Bars", 9)] public bool ColorRVolBars { get; set; } = true;

        [InputParameter("RVOL To Trigger HV Signal", 10)]
        public double RvolHvSignalValue { get; set; } = 3;

        [InputParameter("RVOL To Trigger LV Signal", 11)]
        public double RvolLvSignalValue { get; set; } = 0.15;

        [InputParameter("MA Length", 13, 1, 100)]
        public int MaLength { get; set; } = 20;

        public override string SourceCodeLink => "https://github.com/Lu05/QuantowerIndicators";

        private HistoricalData _additionalData;
        private readonly LineSeries _lineSeries;

        public VolumeBars()
        {
            Name = "Volume Bars - Tradingriot";
            _lineSeries = AddLineSeries("Histogram Bars", Color.FromArgb(75, 112, 128, 144), 1, LineStyle.Histogramm);
            SeparateWindow = true;
            IsUpdateTypesSupported = false;
        }

        protected override void OnInit()
        {
            _additionalData = Symbol.GetHistory(HistoricalData.Period, HistoricalData.FromTime.Subtract(HistoricalData.Period.Duration * Math.Max(LookbackBars, MaLength)), HistoricalData.FromTime);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            base.OnUpdate(args);

            var volume = Volume();
            SetValue(volume);
            int barIndex = (int)HistoricalData.GetIndexByTime(Time().Ticks);

            if (ColorLowVolumeBars && IsLowestVolumeInsidePeriod(volume, barIndex, LookbackBars))
            {
                var color = IsUpCandle() ? LowVolumeColor.Color1 : LowVolumeColor.Color2;
                _lineSeries.SetMarker(0, color);
                SetBarColor(color);
                return;
            }

            if (ColorHighVolumeBars && IsHighestVolumeInsidePeriod(volume, barIndex, LookbackBars))
            {
                var color = IsUpCandle() ? HighVolumeColor.Color1 : HighVolumeColor.Color2;
                _lineSeries.SetMarker(0, color);
                SetBarColor(color);
                return;
            }

            if (ColorRVolBars)
            {
                var sma = GetSmaValue(barIndex, MaLength);
                var rvol = volume / sma;

                if (rvol > RvolHvSignalValue)
                {
                    var color = IsUpCandle() ? HighVolumeColor.Color1 : HighVolumeColor.Color2;
                    _lineSeries.SetMarker(0, color);
                    SetBarColor(color);
                    return;
                }

                if (rvol < RvolLvSignalValue)
                {
                    var color = IsUpCandle() ? LowVolumeColor.Color1 : LowVolumeColor.Color2;
                    _lineSeries.SetMarker(0, color);
                    SetBarColor(color);
                    return;
                }
            }

            if (ColorChurnVolumeBars && (IsOneDayChurnBar(barIndex) || IsTwoDayChurnBar(barIndex)))
            {
                var color = IsUpCandle() ? ChurnBarColor.Color1 : ChurnBarColor.Color2;
                _lineSeries.SetMarker(0, color);
                SetBarColor(color);
                return;
            }

            //set the default color
            SetBarColor();
            _lineSeries.SetMarker(0, _lineSeries.Color);
        }

        private bool IsTwoDayChurnBar(int barIndex)
        {
            var periodRange = GetHighestHigh(barIndex, 2) - GetLowestLow(barIndex, 2);
            var twoDayVolume = Volume() + GetItem(barIndex + 1, PriceType.Volume);
            var value = twoDayVolume / periodRange;

            for (int i = 1; i < LookbackBars; i++)
            {
                periodRange = GetHighestHigh(barIndex + i, 2) - GetLowestLow(barIndex + i, 2);
                twoDayVolume = GetItem(barIndex + i, PriceType.Volume) + GetItem(barIndex + i + 1, PriceType.Volume);
                var currentValue = twoDayVolume / periodRange;

                if (currentValue > value)
                {
                    return false;
                }
            }

            return true;
        }

        private double GetItem(int barIndex, PriceType priceType)
        {
            if (barIndex >= HistoricalData.Count)
            {
                return _additionalData[barIndex - HistoricalData.Count][priceType];
            }

            return HistoricalData[barIndex][priceType];
        }

        private bool IsOneDayChurnBar(int barIndex)
        {
            var volume = Volume();
            var high = High();
            var low = Low();
            var range = high - low;
            var value = volume / range;

            for (int i = 1; i < LookbackBars; i++)
            {
                var offset = barIndex + i;
                volume = GetItem(offset, PriceType.Volume);
                high = GetItem(offset, PriceType.High);
                low = GetItem(offset, PriceType.Low);
                range = high - low;
                var newValue = volume / range;
                if (newValue > value)
                {
                    return false;
                }
            }

            return true;
        }

        private double GetSmaValue(int barIndex, int maLength)
        {
            double sum = 0;
            for (int i = 0; i < maLength; i++)
            {
                var offset = barIndex + i;
                var volume = GetItem(offset, PriceType.Volume);
                sum += volume;
            }

            return sum / maLength;
        }

        private bool IsUpCandle()
        {
            return Open() < Close();
        }

        private bool IsHighestVolumeInsidePeriod(double value, int barIndex, int lookback)
        {
            for (int i = 1; i < lookback; i++)
            {
                var offset = barIndex + i;
                var volume = GetItem(offset, PriceType.Volume);
                if (volume > value)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLowestVolumeInsidePeriod(double value, int barIndex, int lookback)
        {
            for (int i = 1; i < lookback; i++)
            {
                var offset = barIndex + i;
                var volume = GetItem(offset, PriceType.Volume);
                if (volume < value)
                {
                    return false;
                }
            }

            return true;
        }

        private double GetLowestLow(int barIndex, int lookback)
        {
            var low = GetItem(barIndex, PriceType.Low);

            for (int i = 1; i < lookback; i++)
            {
                var offset = barIndex + i;
                var currentLow = GetItem(offset, PriceType.Low);
                if (currentLow < low)
                {
                    low = currentLow;
                }
            }

            return low;
        }

        private double GetHighestHigh(int barIndex, int lookback)
        {
            var high = GetItem(barIndex, PriceType.High);

            for (int i = 1; i < lookback; i++)
            {
                var offset = barIndex + i;

                var currentHigh = GetItem(offset, PriceType.High);
                if (currentHigh > high)
                {
                    high = currentHigh;
                }
            }

            return high;
        }

        protected override void OnClear()
        {
            base.OnClear();
            _additionalData?.Dispose();
        }
    }
}
