#region Licence
//The MIT License (MIT)
//Copyright (c) 2014 abdallah HACID, https://www.facebook.com/ab.hacid

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software
//and associated documentation files (the "Software"), to deal in the Software without restriction,
//including without limitation the rights to use, copy, modify, merge, publish, distribute,
//sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
//is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or
//substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
//BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
//DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// Project Hosting for Open Source Software on Github : https://github.com/abhacid/Robot_Forex
#endregion


#region Description
//
// Le projet et sa description se trouvent sur Github à l'adresse https://github.com/abhacid/Martingale_Forex
//
// Ce projet permet d'écrire un robot de trading basé sur un exemple Robot_Forex initial écrit par 
// imWald sur le dépôt de code source CTDN.
//
// Pour résumer c'est une martingale avec stop loss, money management et stop suiveur.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Lib;
using cAlgo.Strategies;

namespace cAlgo.Robots
{
    [Robot("Martingale Forex", AccessRights = AccessRights.None)]
    public class Martingale_Forex : Robot
    {
        #region cBot Parameters
        [Parameter("Martingale Buy", DefaultValue = true)]
        public bool MartingaleBuy { get; set; }

        [Parameter("Martingale", DefaultValue = 0.5, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Bollinger Division", DefaultValue = 8, MinValue = 2)]
        public int BollingerDivision { get; set; }

        [Parameter("Money Management (%)", DefaultValue = 1.6, MinValue = 0)]
        public double MoneyManagement { get; set; }

        [Parameter("Take Profit", DefaultValue = 5, MinValue = 5)]
        public double TakeProfit { get; set; }

        [Parameter("Stop Loss", DefaultValue = 27.5, MinValue = 0.5)]
        public double StopLoss { get; set; }

        [Parameter("Max Orders", DefaultValue = 2, MinValue = 2)]
        public int MaxOrders { get; set; }

        #endregion

        #region cBot variables

        private bool _isRobotStopped;
        private string _botName;
        private string _botVersion = Assembly.GetExecutingAssembly().FullName.Split(',')[1].Replace("Version=", "").Trim();

        // le label permet de s'y retrouver parmis toutes les instances possibles.
        private string _instanceLabel;

        // Est une suite d'achat (Buy) ou une suite de vente (Sell).
        private TradeType? _tradesType = null;

        // premier volume utilisé.
        private double _firstVolume;
        List<Strategy> _strategies;
        private StaticPosition _cornerPosition;
        private bool _debug;
        private int _nPositions;
        #endregion

        protected override void OnStart()
        {
            base.OnStart();

            _debug = true;
            _botName = ToString();
			_instanceLabel = string.Format("{0}-{1}-{2}-{3}", _botName, _botVersion, Symbol.Code, TimeFrame.ToString());
			_nPositions = Positions.FindAll(_instanceLabel).Length;

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;

            _strategies = new List<Strategy>();
            _strategies.Add(new DoubleCandleStrategy(this, 14, 0, BollingerDivision));

            int corner = 1;

            switch (corner)
            {
                case 1:
                    _cornerPosition = StaticPosition.TopLeft;
                    break;
                case 2:
                    _cornerPosition = StaticPosition.TopRight;
                    break;
                case 3:
                    _cornerPosition = StaticPosition.BottomLeft;
                    break;
                case 4:
                    _cornerPosition = StaticPosition.BottomRight;
                    break;
            }

            ChartObjects.DrawText("BotVersion", _botName + " Version : " + _botVersion, _cornerPosition);

            if (_debug)
            {
                Print("The current symbol has PipSize of: {0}", Symbol.PipSize);
                Print("The current symbol has PipValue of: {0}", Symbol.PipValue);
                Print("The current symbol has TickSize: {0}", Symbol.TickSize);
                Print("The current symbol has TickSValue: {0}", Symbol.TickValue);
            }
        }

        // Méthode de callback sur chaque tick
        protected override void OnTick()
        {
            if (Trade.IsExecuting)
                return;

            if (_nPositions > 0 && _isRobotStopped)
                return;
            else
                _isRobotStopped = false;

            if (_nPositions == 0)
            {
                // Calcule le volume en fonction du money management pour un risque maximum et un stop loss donné.
                // Ne tient pas compte des risques sur d'autres positions ouvertes du compte de trading utilisé
                double maxVolume = this.moneyManagement(MoneyManagement, StopLoss);

                if (MartingaleBuy)
                    _firstVolume = maxVolume;
                else
                    _firstVolume = maxVolume / (MaxOrders + (MartingaleCoeff * MaxOrders * (MaxOrders - 1)) / 2.0);

                Debug.Assert(_firstVolume > 0, "the 'first lot' : {0} parameter must be positive and not null");

                executeFirstOrder(_firstVolume);
            }
            else
                ControlSeries();
        }

        protected override void OnError(Error error)
        {
            string errorString = this.errorString(error);

            if (errorString != "")
            {
                _isRobotStopped = true;
                Print(errorString);
            }

        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            _nPositions++;
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            _nPositions--;
        }

        private void executeFirstOrder(double volume)
        {
            _tradesType = this.signal(_strategies);

			if(_tradesType.HasValue && (Positions.Find(_instanceLabel,Symbol,_tradesType.Value) == null))
				executeOrder(_tradesType, volume);
        }

        private void ControlSeries()
        {
            Debug.Assert(_nPositions > 0);

            if (_nPositions < MaxOrders)
            {
                double volume = _firstVolume * (1 + MartingaleCoeff * _nPositions);

                double priceStep = MarketSeries.volatility(14) / MaxOrders;

                if (MartingaleBuy)
                {
                    double? bestPrice = GetBestPrice();

                    switch (_tradesType)
                    {
                        case TradeType.Buy:
                            if (Symbol.Ask >= bestPrice.Value + priceStep * _nPositions)
                                executeOrder(TradeType.Buy, volume);
                            break;

                        case TradeType.Sell:
                            if (Symbol.Bid <= bestPrice.Value - priceStep * _nPositions)
                                executeOrder(TradeType.Sell, volume);
                            break;
                    }
                }
                else
                {
                    double? worsePrice = GetWorsePrice();

                    switch (_tradesType)
                    {
                        case TradeType.Buy:
                            if (Symbol.Ask <= worsePrice.Value - priceStep * _nPositions)
                                executeOrder(TradeType.Buy, volume);
                            break;

                        case TradeType.Sell:
                            if (Symbol.Bid >= worsePrice.Value + priceStep * _nPositions)
                                executeOrder(TradeType.Sell, volume);
                            break;
                    }
                }
            }

            //if (!DEBUG)
            //	ChartObjects.DrawText("MaxDrawdown", "MaxDrawdown: " + Math.Round(GetMaxDrawdown(), 2) + " Percent", corner_position);
        }

        private TradeResult executeOrder(TradeType? tradeType, double volume)
        {
            if (!(tradeType.HasValue))
                return null;

            long normalizedVolume = Symbol.NormalizeVolume(volume, RoundingMode.ToNearest);
            TradeResult tradeResult = ExecuteMarketOrder(tradeType.Value, Symbol, normalizedVolume, _instanceLabel, StopLoss, TakeProfit, 10, _botName + " v" + _botVersion);

            if (tradeResult.IsSuccessful)
            {
                double? newStopLoss = null;
                double? newTakeProfit = null;
                double averagePrice = GetAveragePrice();

                double priceStep = MarketSeries.volatility(14) / MaxOrders;
                int pipStep = (int)(priceStep / MaxOrders);
                int trailStopMin = (int)(3 * Symbol.Spread / Symbol.PipValue);

                Position[] positions = Positions.FindAll(_instanceLabel, Symbol);

                foreach (Position position in positions)
                {
                    if (MartingaleBuy)
                    {
                        newStopLoss = position.trailStop(this, pipStep, pipStep, trailStopMin, false);
                        if (!(newStopLoss.HasValue))
                            newStopLoss = position.StopLoss;

                        switch (_tradesType)
                        {
                            case TradeType.Buy:
                                newTakeProfit = averagePrice + TakeProfit * Symbol.PipSize;
                                break;
                            case TradeType.Sell:
                                newTakeProfit = averagePrice - TakeProfit * Symbol.PipSize;
                                break;
                        }

                    }
                    else
                    {
                        switch (_tradesType)
                        {
                            case TradeType.Buy:
                                newTakeProfit = averagePrice + TakeProfit * Symbol.PipSize;
                                newStopLoss = averagePrice - StopLoss * Symbol.PipSize;
                                break;
                            case TradeType.Sell:
                                newTakeProfit = averagePrice - TakeProfit * Symbol.PipSize;
                                newStopLoss = averagePrice + StopLoss * Symbol.PipSize;
                                break;
                        }
                    }

                    if (newStopLoss != position.StopLoss || newTakeProfit != position.TakeProfit)
                        ModifyPosition(position, newStopLoss, newTakeProfit);
                }

                if (MartingaleBuy)
                {
                    double? bestPrice = GetBestPrice();

                    switch (_tradesType)
                    {
                        case TradeType.Buy:
                            ChartObjects.DrawHorizontalLine("gridBuyLine", bestPrice.Value + priceStep * _nPositions, Colors.Navy, 2);
                            break;

                        case TradeType.Sell:
                            ChartObjects.DrawHorizontalLine("gridSellLine", bestPrice.Value - priceStep * _nPositions, Colors.Orange, 2);
                            break;
                    }
                }
                else
                {
                    double? worsePrice = GetWorsePrice();

                    switch (_tradesType)
                    {
                        case TradeType.Buy:
                            ChartObjects.DrawHorizontalLine("gridBuyLine", worsePrice.Value - priceStep * _nPositions, Colors.Navy, 2);
                            break;

                        case TradeType.Sell:
                            ChartObjects.DrawHorizontalLine("gridSellLine", worsePrice.Value + priceStep * _nPositions, Colors.Orange, 2);
                            break;
                    }
                }
            }

            return tradeResult;
        }

        private double GetAveragePrice()
        {
            double sum = 0;
            long count = 0;

            foreach (Position position in Positions.FindAll(_instanceLabel))
            {
                sum += position.EntryPrice * position.Volume;
                count += position.Volume;
            }

            if (sum > 0 && count > 0)
                return sum / count;
            else
                throw new System.ArgumentException("GetAveragePrice() : There is no open position");
        }

        private double _savedMaxBalance;
        private List<double> _drawdowns = new List<double>();
        private double GetMaxDrawdown()
        {
            _savedMaxBalance = Math.Max(_savedMaxBalance, Account.Balance);

            _drawdowns.Add((_savedMaxBalance - Account.Balance) / _savedMaxBalance * 100);
            _drawdowns.Sort();

            double maxDrawdown = _drawdowns[_drawdowns.Count - 1];

            return maxDrawdown;
        }

        private double? GetWorsePrice()
        {
            double worsePrice = 0;

            if (_tradesType.HasValue)
                foreach (Position position in Positions.FindAll(_instanceLabel))
                {
                    if (_tradesType.isBuy())
                    {
                        if (position.EntryPrice < worsePrice || worsePrice == 0)
                            worsePrice = position.EntryPrice;
                    }
                    else if (position.EntryPrice > worsePrice || worsePrice == 0)
                        worsePrice = position.EntryPrice;
                }
            else
                return null;

            return worsePrice;
        }

        private double? GetBestPrice()
        {
            double bestPrice = 0;

            if (_tradesType.HasValue)
                foreach (Position position in Positions.FindAll(_instanceLabel))
                {
                    if (_tradesType.isBuy())
                    {
                        if (position.EntryPrice > bestPrice || bestPrice == 0)
                            bestPrice = position.EntryPrice;
                    }
                    else if (position.EntryPrice < bestPrice || bestPrice == 0)
                        bestPrice = position.EntryPrice;
                }
            else
                return null;

            return bestPrice;
        }


    }
}
