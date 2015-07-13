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
using System.Text;
using System.Threading;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Lib;
using cAlgo.Strategies;

namespace cAlgo.Robots
{
    [Robot("Martingale Forex", AccessRights = AccessRights.None, TimeZone=TimeZones.UTC)]
    public class Martingale_Forex : Robot
    {
        #region cBot Parameters

        [Parameter("Martingale Coefficient", DefaultValue = 0.5, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Grid Step", DefaultValue = 11, MinValue = 0.1)]
        public double GridStep { get; set; }

        [Parameter("Max Orders", DefaultValue = 20, MinValue = 1)]
        public int MaxOrders { get; set; }

        [Parameter("Risk (%)", DefaultValue = 2, MinValue = 0.01)]
        public double Risk { get; set; }

        [Parameter("Stop Loss", DefaultValue = 30, MinValue = 1)]
        public double StopLoss { get; set; }

		[Parameter("Trail Start", DefaultValue = 17, MinValue = 0)]
		public double TrailStart{ get; set; }

		[Parameter("Trail Stop", DefaultValue = 11, MinValue = 0)]
		public double TrailStop{ get; set; }

        [Parameter("Global Timeframe")]
        public TimeFrame GlobalTimeFrame { get; set; }

        [Parameter("Global Candle Ceil", DefaultValue =0, MinValue = 0)]
		public int MinimumGlobalCandleCeil { get; set; }

		[Parameter("UTC+0 Trade Start", DefaultValue = 7.0)]
		public double TimeStart { get; set; }

		[Parameter("UTC+0 Trade Stop", DefaultValue = 17.0)]
		public double TimeStop { get; set; }

        #endregion

        #region cBot variables

        private string _botName;
        private string _botVersion = Assembly.GetExecutingAssembly().FullName.Split(',')[1].Replace("Version=", "").Trim();
        private string _instanceLabel;

        CandlestickTendencyII _signalIndicator;
        private bool _debug;
        bool? _isBuy;
        int? _factor;
        double _gridStep;
        double _actualGainOrLoss = 0;

		private Object _LockControlSeries = new Object();
		private bool _isInOnTick;

		DateTime _startTime;
		DateTime _endTime;

        #endregion

        #region cBot Events

        protected override void OnStart()
        {
            base.OnStart();

            _debug = true;

            _botName = ToString();
            _instanceLabel = string.Format("{0}-{1}-{2}-{3}", _botName, _botVersion, Symbol.Code, TimeFrame.ToString());
            _gridStep = GridStep * Symbol.PipSize;
			_isInOnTick = false;

			_startTime = Server.Time.Date.AddHours(TimeStart);
			_endTime = Server.Time.Date.AddHours(TimeStop);

            if (isExistPositions())
            {
                Position anyPosition = firstPosition();
                _isBuy = anyPosition.isBuy();
                _factor = anyPosition.factor();
            }
			else
			{
				_isBuy = null;
				_factor = null;
			}

            ChartObjects.DrawText("BotVersion", _botName + " " + _botVersion, StaticPosition.TopCenter);
            if (_debug)
            {
                Print("The current symbol is {0}", Symbol.Code);
                Print("The current symbol has PipSize (deposit currency) of: {0}", Symbol.PipSize);
                Print("The current symbol has PipValue (quote currency) of: {0}", Symbol.PipValue);
                Print("The current symbol has TickSize (deposit currency): {0}", Symbol.TickSize);
                Print("The current symbol has TickSValue (quote currency): {0}", Symbol.TickValue);
                Print("The current symbol has {0} Digits", Symbol.Digits);
                Print("The current symbol minimum baseVolume is {0}", Symbol.VolumeMin);
                Print("The current symbol maximum baseVolume is {0}", Symbol.VolumeMax);
                Print("The current symbol step baseVolume is {0}", Symbol.VolumeStep);
            }

			_signalIndicator = Indicators.GetIndicator<CandlestickTendencyII>(GlobalTimeFrame, MinimumGlobalCandleCeil);

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;
        }

		protected override void OnStop()
		{
			base.OnStop();

			this.closeAllPositions(_instanceLabel); // For test only
		}

        /// <summary>
        /// Méthode de callback sur chaque tick.
		/// L'instruction lock permet de créer une file d'attente des threads souhaitant entrer dans
		/// ce bloc de code afin que seul un thread à la fois puisse exécuter ce bloc.
        /// </summary>
        protected override void OnTick()
        {
            base.OnTick();

			if(_isInOnTick)
				return;

			lock(_LockControlSeries)
			{
				_isInOnTick = true;

				manageStopLoss();

				_isInOnTick = false;
			}

        }

		protected override void OnBar()
		{
			base.OnBar();

			ControlSeries();

		}

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args != null && args.Position != null)
                _actualGainOrLoss += args.Position.NetProfit;

			if (isNotExistPositions())
			{
				_isBuy = null;
				_factor = null;
			}

        }
        protected override double GetFitness(GetFitnessArgs args)
        {
            //maximize count of winning trades and minimize count of losing trades
            return args.WinningTrades / args.LosingTrades;
        }

        protected override void OnError(Error error)
        {
            string errorString = this.errorString(error);

            if (errorString != "")
                Print(errorString);
        }
        #endregion

        /// <summary>
        /// Gère le control des différentes prises de positions. un seul type de position est pris, le baseVolume des 
        /// positions successives augmentent selon le coefficient de martingale de manière linéaire en cas de pertes 
        /// (MartingaleGain=false). Les nouvelles positions sont prises selon la grille _gridStep dont la 
        /// valeur dépends de la volatilité. Lors d'une nouvelle position dans le cas d'une martingale sur les gains, 
        /// les stops loss des positions précédentes sont ramenées au prix d'achat ou de vente afin d'assurer des stops 
        /// zéro, puis déplacés au prochain prix d'achat ou de vente suivant si la position continue d'être gagnante.
        /// 
        /// Calcule le baseVolume d'achat ou de vente en fonction du money management pour un risque maximum et un stop loss donné.
        ///	Ne tient pas compte des risques sur d'autres positions ouvertes du compte de trading utilisé
        /// </summary>
        private void ControlSeries()
        {
            TradeResult tradeResult;

            if (!_isBuy.HasValue)
            {
                tradeResult = manageFirstOrder();

                if (tradeResult != null && tradeResult.IsSuccessful)
                {
                    _isBuy = tradeResult.Position.isBuy();
                    _factor = tradeResult.Position.factor();
                }
            }
            else
            {
				drawSystemInfos();
				drawPositionsInfos();

                tradeResult = manageNextOrder();

				TradeType? tradeType=signal();
				if (tradeType.HasValue)
				{
					TradeType? actualTradeType=tradesType();
					if (actualTradeType.HasValue && tradeType != actualTradeType)
					{
						foreach(Position position in Positions)
							ClosePosition(position);
					}
				}
            }
        }

        /// <summary>
        /// Manage the first order.
        /// </summary>
        /// <returns></returns>
        private TradeResult manageFirstOrder()
        {
            TradeResult tradeResult = null;
			TradeType? tradeTypeSignal = signal();

            if (tradeTypeSignal.HasValue)
                tradeResult = executeOrder(tradeTypeSignal);

            return tradeResult;
        }

        /// <summary>
        /// return a buy or a sell signal, or null if there is no signal.
        /// </summary>
        /// <returns></returns>
        private TradeType? signal()
        {
			// 'Count-1' is the index of the last candle so 'Count-2' is the preview candle
			// You cannot use 'Count-1' because the Close property is not defined for the last active candle
			int index = MarketSeries.Close.Count - 1;
			TradeType? tradeType = null;

			bool isGlobalUp = _signalIndicator.GlobalTrendSignal[index]> 0;
			bool isLocalUp = _signalIndicator.LocalTrendSignal[index] > 0;
			bool isGlobalDn = _signalIndicator.GlobalTrendSignal[index] < 0;
			bool isLocalDn = _signalIndicator.LocalTrendSignal[index] < 0;

			if(( isGlobalUp) && (isLocalUp))
				tradeType = TradeType.Buy;
			if((isGlobalDn) && (isLocalDn))
				tradeType = TradeType.Sell;

            return tradeType;
        }

        /// <summary>
        /// Manage the others orders.
        /// </summary>
        /// <returns></returns>
        private TradeResult manageNextOrder()
        {
            if (nPositions() >= MaxOrders || !_isBuy.HasValue)
                return null;

            TradeResult tradeResult = null;

            double upPrice = upEntryPrice();
            double dnPrice = dnEntryPrice();
            double actualPrice = _isBuy.Value ? Symbol.Ask : Symbol.Bid;

            if (((actualPrice >= upPrice + _gridStep) && _isBuy.Value) || ((actualPrice <= dnPrice - _gridStep) && !(_isBuy.Value)))
                tradeResult = executeOrder(tradesType());

            return tradeResult;
        }

        /// <summary>
        /// Execute an order of type "tradesType"
        /// </summary>
        /// <param name="tradesType"></param>
        /// <returns></returns>
        private TradeResult executeOrder(TradeType? tradeType)
        {
            if (!(tradeType.HasValue))
                return null;

            TradeResult tradeResult = null;

            double volume = computeVolume();
            if (volume >= Symbol.VolumeMin)
                tradeResult = ExecuteMarketOrder(tradeType.Value, Symbol, Symbol.NormalizeVolume(volume, RoundingMode.ToNearest), _instanceLabel, StopLoss, null, 10, comment());


            return tradeResult;
        }

        /// <summary>
        /// Manage the stop Loss with a trailstop.
        /// </summary>
        /// <param name="baseVolume"></param>
        /// <returns></returns>
        private void manageStopLoss()
        {
            foreach (Position position in Positions.FindAll(_instanceLabel, Symbol))
            {
				if(!_isBuy.HasValue)
					return;

				double newStopLoss = 10000 * (_isBuy.Value ? -1 : 1); //averagePrices() - (_factor * (1 - ((double)nPositions() / MaxOrders))) * Symbol.PipSize * StopLoss;
                double price = (_isBuy.Value ? Symbol.Bid : Symbol.Ask);

				if (_isBuy.Value)
				{
					if ((price - position.EntryPrice) >  TrailStart * Symbol.PipSize + Symbol.Spread)
						newStopLoss = price - TrailStop * Symbol.PipSize - Symbol.Spread;
				}
				else
				{
					if ((position.EntryPrice - price) >  TrailStart * Symbol.PipSize + Symbol.Spread)
						newStopLoss = price + TrailStop * Symbol.PipSize + Symbol.Spread;
				}

				//else
				//if((price - position.EntryPrice) * _factor > TrailStart* Symbol.PipSize)
				//	newStopLoss = price - _factor * TrailStop * Symbol.PipSize;
				//else
					//

                if ((newStopLoss.round(this) - position.StopLoss) * _factor > 0)
                    modifyOrder(position, newStopLoss, position.TakeProfit);
            }
        }

        /// <summary>
        /// Modify the stoploss and takeprofit of a taking position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="stopLoss"></param>
        /// <param name="takeProfit"></param>
        /// <returns></returns>
        private TradeResult modifyOrder(Position position, double? stopLoss, double? takeProfit)
        {
            if (position.StopLoss != stopLoss || position.TakeProfit != takeProfit)
                return ModifyPosition(position, stopLoss, takeProfit);

            return null;
        }

        /// <summary>
        /// Add an index of the position in the comment position.
        /// </summary>
        /// <returns></returns>
        private string comment()
        {
            StringBuilder comment = new StringBuilder();

            if (_isBuy.HasValue)
            {
                Position firstPosition = Positions.Find(_instanceLabel);
                int indexPositions = Positions.FindAll(_instanceLabel).Length + 1;
                comment.AppendFormat("{0}-{1}", firstPosition.Id.ToString(), indexPositions.ToString());
            }

            return comment.ToString();
        }

        /// <summary>
        /// Return the type Buy or Sell of the positions series.
        /// </summary>
        /// <returns></returns>
        private TradeType? tradesType()
        {
            TradeType? tradeType = null;

            if (_isBuy.HasValue)
            {
                Position anyPosition = Positions.Find(_instanceLabel);
                tradeType = anyPosition.TradeType;
            }

            return tradeType;

        }

        /// <summary>
        /// Return the first position of all taking positions.
        /// </summary>
        /// <returns></returns>
        private Position firstPosition()
        {
            return Positions.Find(_instanceLabel);
        }

        /// <summary>
        /// Return true if there is no position or false if there is position.
        /// </summary>
        /// <returns></returns>
        private bool isNotExistPositions()
        {
            return !isExistPositions();
        }

        /// <summary>
        ///  Return true if there is position or false if there is no position.
        /// </summary>
        /// <returns></returns>
        private bool isExistPositions()
        {
            return nPositions() > 0;
        }

        /// <summary>
        /// Return the numbers of positions.
        /// </summary>
        /// <returns></returns>
        private int nPositions()
        {
            Position[] positions = Positions.FindAll(_instanceLabel);

            if (positions != null)
                return positions.Length;
            else
                return 0;
        }

        /// <summary>
        /// Draw infos of the positions series and account.
        /// </summary>
        private void drawSystemInfos()
        {
            if (!_isBuy.HasValue)
                return;

            double price = _isBuy.Value ? Symbol.Ask : Symbol.Bid;
            double upPrice = upEntryPrice();
            double dnPrice = dnEntryPrice();
            double pipsForNextOrder = 0;
            double priceOfNextOrder = 0;

            if (_isBuy.Value)
                priceOfNextOrder = upPrice + _gridStep;
            else
                priceOfNextOrder = dnPrice - _gridStep;

            pipsForNextOrder = priceOfNextOrder - price;

			// LotSize is a feature of 1.30.58489 version of cAlgo
			long LotSize = 100000;

            string textToPrint = 
				"\nSpread\t\t: " + Math.Round(Symbol.Spread * Math.Pow(10, Symbol.Digits), 2) + " ticks" + 
				"\nAsk\t\t: " + Math.Round(Symbol.Ask, Symbol.Digits) + 
				"\nMid\t\t: " + Math.Round((Symbol.Ask + Symbol.Bid) / 2, Symbol.Digits) + 
				"\nBid\t\t: " + Math.Round(Symbol.Bid, Symbol.Digits) + 
				"\n-----------------" + 
				"\nMax Orders\t: " + MaxOrders + 
				"\nPotential Gain\t: indetermined" + 
				"\nPotential Loss\t: " + Math.Round(this.potentialLoss(_instanceLabel), 2) + 
				"\nGain or Loss\t: " + Math.Round(_actualGainOrLoss, 2) + " Euros" + 
				"\n-----------------" + 
				"\nnext step in\t: " + Math.Round(pipsForNextOrder * Math.Pow(10, Symbol.Digits), 2) + 
				" ticks" + "\nUp Price\t\t: " + Math.Round(upPrice, Symbol.Digits) + 
				"\nDn Price\t\t: " + Math.Round(dnPrice, Symbol.Digits) + 
				"\nNext Order Price\t: " + Math.Round(priceOfNextOrder, Symbol.Digits) + 
				"\nNext Price\t: " + Math.Round(priceOfNextOrder, Symbol.Digits) + 
				"\nNext Volume\t: " + Math.Round(computeVolume() / LotSize /*Symbol.LotSize*/, 2) + " lots";

            ChartObjects.DrawHorizontalLine("gridLine", priceOfNextOrder, Colors.Navy, 2);

            ChartObjects.DrawText("systemInfos", textToPrint, StaticPosition.TopLeft);
        }

        /// <summary>
        /// Draw infos about the positions.
        /// </summary>
        private void drawPositionsInfos()
        {
            if (!_isBuy.HasValue)
                return;

            StringBuilder positionsInfos = new StringBuilder();
            string format = "{0} \t| {1} \t| {2}\n";

            positionsInfos.AppendFormat(format, "Id", "potential loss", "Pips");

            foreach (Position position in Positions.FindAll(_instanceLabel))
            {
				if( position.StopLoss.HasValue)
				{
					double potentialLoss = Math.Round((position.EntryPrice - position.StopLoss.Value) * position.Volume, 2);
					double pipsToStopLoss = Math.Round(position.stopLossToPips(Symbol).Value, 2);

					positionsInfos.AppendFormat(format, position.Id, potentialLoss, pipsToStopLoss);				
				}

            }

            ChartObjects.DrawText("positionsInfos", positionsInfos.ToString(), StaticPosition.TopRight);
        }

        /// <summary>
        /// Calculate the volume of the position to take with money management considerations.
        /// </summary>
        /// <returns></returns>
        private double computeVolume()
        {
			double maxVolume = this.moneyManagement(Risk, StopLoss);

			double nPosition = nPositions();
			double nRemainingOrders = MaxOrders - nPosition;
			double alpha = nRemainingOrders / MaxOrders;

			double baseVolume = maxVolume / ((nRemainingOrders + 1) + (MartingaleCoeff * (nRemainingOrders + 1) * nRemainingOrders / 2.0));

			baseVolume = alpha * baseVolume + (1 - alpha) * maxVolume;

			double volume = baseVolume * (1 + MartingaleCoeff * nPosition);

			return volume;
        }

        /// <summary>
        /// Calcule la moyenne des prises de positions (prix moyen), c'est un barycentre des 
        /// prix pondéré par les volumes correspondants.
        /// </summary>
        /// <returns></returns>
        private double averagePrices()
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

            return 0;
        }

        /// <summary>
        /// Return the position that have the high entry price.
        /// </summary>
        /// <returns></returns>
        private Position upPosition()
        {
            Position upPosition = null;
            double price = 0;

            foreach (Position position in Positions.FindAll(_instanceLabel))
            {
                if (position.EntryPrice > price || price == 0)
                {
                    price = position.EntryPrice;
                    upPosition = position;
                }
            }

            return upPosition;
        }

        /// <summary>
        /// Return the position that have the low entry price.
        /// </summary>
        /// <returns></returns>
        private Position dnPosition()
        {
            Position dnPosition = null;
            double price = 0;

            foreach (Position position in Positions.FindAll(_instanceLabel))
            {
                if (position.EntryPrice < price || price == 0)
                {
                    price = position.EntryPrice;
                    dnPosition = position;
                }
            }

            return dnPosition;
        }

        /// <summary>
        /// Return the high entry price.
        /// </summary>
        /// <returns></returns>
        private double upEntryPrice()
        {
            Position position = upPosition();

            if (position != null)
                return position.EntryPrice;

            return 0;
        }

        /// <summary>
        /// return the low entry price.
        /// </summary>
        /// <returns></returns>
        private double dnEntryPrice()
        {
            Position position = dnPosition();

            if (position != null)
                return position.EntryPrice;

            return 0;
        }

		private bool isTimeToTrade()
		{
			bool isTimeToTrade = (Server.Time.TimeOfDay >= _startTime.TimeOfDay) && (Server.Time.TimeOfDay <= _endTime.TimeOfDay);

			if(!isTimeToTrade)
			{
				Print("Start time : {0}, server time : {1}, finish time : {2}", _startTime, Server.Time, _endTime);
				return false;
			}
			else
				return true;
			

		}
    }
}
