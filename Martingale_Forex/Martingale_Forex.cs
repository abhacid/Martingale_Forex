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

// Project Hosting for Open Source Software on Github : https://github.com/abhacid/Martingale_Forex
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
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Lib;
using cAlgo.Strategies;

namespace cAlgo.Robots
{
    [Robot("Martingale Forex", AccessRights = AccessRights.None, TimeZone=TimeZones.UTC)]
    public class Martingale_Forex : Robot
    {
        #region cBot Parameters

		[Parameter("UTC+0 Trade Start", DefaultValue = 0.0)]
		public double TimeStart { get; set; }

		[Parameter("UTC+0 Trade Stop", DefaultValue = 24.0)]
		public double TimeStop { get; set; } 
       
        [Parameter("Grid Step", DefaultValue = 11, MinValue = 0.1)]
        public int GridStep { get; set; }

		[Parameter("Martingale Coefficient", DefaultValue = 0.5, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Max Orders", DefaultValue = 20, MinValue = 1)]
        public int MaxOrders { get; set; }

        [Parameter("Risk (%)", DefaultValue = 2, MinValue = 0.01)]
        public double RiskPercent { get; set; }
		
		//[Parameter("Stop Loss", DefaultValue = 30, MinValue = 1)]
		//public int _stopLoss { get; set; }

		//[Parameter("Trail Start", DefaultValue = 17, MinValue = 0)]
		//public int TrailStart { get; set; }

		//[Parameter("Trail Stop", DefaultValue = 11, MinValue = 0)]
		//public int TrailStop { get; set; }

		[Parameter("ATR Period", DefaultValue = 14)]
		public int AtrPeriod { get; set; }

		[Parameter("ATR Moving Average Type", DefaultValue = MovingAverageType.Exponential)]
		public MovingAverageType AtrMovingAverageType { get; set; }

		[Parameter("ATR Stop Loss Coefficient", DefaultValue = 1)]
		public double AtrStopLossCoefficient { get; set; }


		[Parameter("Reverse On Signal", DefaultValue = false)]
		public bool ReverseInOppositeSignal { get; set; }

        [Parameter("Global Timeframe")]
        public TimeFrame GlobalTimeFrame { get; set; }

        [Parameter("Global MA Ceil Coefficient", DefaultValue = 0.618)]
        public double GlobalCeilCoefficient { get; set; }

        [Parameter("Global Candle Ceil", DefaultValue =0, MinValue = 0)]
		public int MinimumGlobalCandleCeil { get; set; }

        [Parameter("Weak Volume (%)", DefaultValue = 10, MinValue = 0)]
        public double WeakVolumePercent { get; set; }



        #endregion

        #region cBot variables

        private string _botName;
        private string _botVersion = Assembly.GetExecutingAssembly().FullName.Split(',')[1].Replace("Version=", "").Trim();
        private string _instanceLabel;
		enum SignalType { StrongBuy, StrongSell, StrongNeutral, WeakBuy, WeakSell, WeakNeutral, Neutral}

		AverageTrueRange _atr;
        CandlestickTendencyII _signalIndicator;
        private bool _debug;
        bool? _isBuy;
        int? _factor;
		SignalType _signalType;
        double _gridStep;
        double _actualGainOrLoss = 0;

		private Object _LockControlSeries = new Object();
		private bool _isInOnTick;

		DateTime _startTime;
		DateTime _endTime;

        #endregion

        #region cBot Events

		#region Properties
		/// <summary>
		/// Calculate the stoploss in correlation with Average True Range (ATR)
		/// </summary>
		private double? StopLoss
		{
			
			get{ return _atr.Result.lastRealValue(0) * AtrStopLossCoefficient; }
		}

		/// <summary>
		/// Calculate the volume of the position to take with money management considerations.
		/// </summary>
		/// <returns></returns>
		private double Volume
		{
			get
			{
				double maxVolume = this.moneyManagement(RiskPercent, StopLoss, false);

				double baseVolume = maxVolume / ((MaxOrders + 1) + (MartingaleCoeff * MaxOrders * (MaxOrders + 1)) / 2.0);

				double volume = baseVolume * (1 + MartingaleCoeff * nPositions());

				return volume;
			}

		}

		/// <summary>
		/// Add an index to the comment position.
		/// </summary>
		/// <returns></returns>
		private string Comment
		{
			get
			{
				StringBuilder comment = new StringBuilder(_signalType.ToString());

				if(_isBuy.HasValue)
				{
					Position firstPosition = Positions.Find(_instanceLabel);
					int indexPositions = Positions.FindAll(_instanceLabel).Length + 1;
					string format = (_signalType.ToString().Length != 0) ? "-{0}-{1}" : "{0}-{1}";

					comment.AppendFormat(format, firstPosition.Id.ToString(), indexPositions.ToString());
				}

				return comment.ToString();			
			}

		}

		/// <summary>
		/// Return the type Buy or Sell of the positions series.
		/// </summary>
		/// <returns></returns>
		private TradeType? TradesType
		{
			get
			{
			TradeType? tradeType = null;

			if(_isBuy.HasValue)
			{
				Position anyPosition = Positions.Find(_instanceLabel);
				tradeType = anyPosition.TradeType;
			}

			return tradeType;			
			}
		}

		/// <summary>
		/// Return the first position of all taking positions.
		/// </summary>
		/// <returns></returns>
		private Position FirstPosition
		{
			get{return Positions.Find(_instanceLabel);}
		}

		#endregion


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
                Position firstTradedPosition = FirstPosition;
                _isBuy = firstTradedPosition.isBuy();
                _factor = firstTradedPosition.factor();

				if(!(Enum.TryParse<SignalType>(firstTradedPosition.Comment, out _signalType)))
					_signalType = SignalType.Neutral;
            }
			else
			{
				_isBuy = null;
				_factor = null;
				_signalType = SignalType.Neutral;
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

			MarketSeries globalMarketSeries = MarketData.GetSeries(GlobalTimeFrame);
			_atr = Indicators.AverageTrueRange(globalMarketSeries, 14, MovingAverageType.Exponential);
			_signalIndicator = Indicators.GetIndicator<CandlestickTendencyII>(GlobalTimeFrame, MinimumGlobalCandleCeil);

            Positions.Opened += OnPositionOpened;
            Positions.Closed += OnPositionClosed;
        }

		protected override void OnStop()
		{
			base.OnStop();

			//this.closeAllPositions(_instanceLabel); // For test only
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
				_signalType = SignalType.Neutral;
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
				if(isTimeToTrade())
				{
					tradeResult = manageFirstOrder(Volume);

					if(tradeResult != null && tradeResult.IsSuccessful)
					{
						_isBuy = tradeResult.Position.isBuy();
						_factor = tradeResult.Position.factor();
					}
				}
            }
            else
            {
				drawSystemInfos();
				drawPositionsInfos();

				if(isTimeToTrade())
					tradeResult = manageNextOrder(Volume);

				if (ReverseInOppositeSignal)
				{
					SignalType newSignalType = determineSignal();
					if(!isNeutralSignal(newSignalType))
					{
						if ((_signalType == SignalType.StrongBuy && newSignalType == SignalType.StrongSell) || 
							(_signalType == SignalType.StrongSell && newSignalType == SignalType.StrongBuy) ||
							(_signalType == SignalType.WeakBuy && isSellSignal(newSignalType)) ||
							(_signalType == SignalType.WeakSell && isBuySignal(newSignalType)))
							
							foreach(Position position in Positions)
								ClosePositionAsync(position); // Async in order to accelerate the close of positions.
					}				
				}

            }
        }

        /// <summary>
        /// Manage the first order.
        /// </summary>
        /// <returns></returns>
        private TradeResult manageFirstOrder(double strongVolume)
        {
            TradeResult tradeResult = null;
			SignalType signalType = determineSignal();
			double volume = strongVolume;

            if (!isNeutralSignal(signalType))
			{
				if(isWeakSignal(signalType))
					volume = strongVolume * WeakVolumePercent / 100;

				tradeResult = executeOrder(tradeType(signalType), volume);

				if (tradeResult!=null && tradeResult.IsSuccessful)
					_signalType = signalType;
			}

            return tradeResult;
        }

        /// <summary>
        /// Manage the others orders.
        /// </summary>
        /// <returns></returns>
        private TradeResult manageNextOrder(double volume)
        {
            if (nPositions() >= MaxOrders || !_isBuy.HasValue)
                return null;

            TradeResult tradeResult = null;

            double upPrice = upEntryPrice();
            double dnPrice = dnEntryPrice();
            double actualPrice = _isBuy.Value ? Symbol.Ask : Symbol.Bid;

            if (((actualPrice >= upPrice + _gridStep) && _isBuy.Value) || ((actualPrice <= dnPrice - _gridStep) && !(_isBuy.Value)))
			{
				double newVolume = (isStrongSignal(_signalType) ? volume : volume * WeakVolumePercent / 100);
                tradeResult = executeOrder(TradesType, newVolume);
			}

            return tradeResult;
        }

        /// <summary>
        /// Execute an order of type "tradesType"
        /// </summary>
        /// <param name="tradesType"></param>
        /// <returns></returns>
        private TradeResult executeOrder(TradeType? tradeType, double volume)
        {
            if (!(tradeType.HasValue))
                return null;

            TradeResult tradeResult = null;

			if(volume >= Symbol.VolumeMin)
			{
				long normalizedVolume = Symbol.NormalizeVolume(volume, RoundingMode.ToNearest);
				tradeResult = ExecuteMarketOrder(tradeType.Value, Symbol, normalizedVolume, _instanceLabel, null, null, 10, Comment);
			}


            return tradeResult;
        }

        /// <summary>
        /// Manage the stop Loss with a trailstop.
        /// </summary>
        /// <param name="baseVolume"></param>
        /// <returns></returns>
        private void manageStopLoss()
        {
			if(!_isBuy.HasValue)
				return;

            foreach (Position position in Positions.FindAll(_instanceLabel, Symbol))
            {
				double newStopLoss = (_isBuy.Value ? Symbol.Bid : Symbol.Ask) - _factor.Value * StopLoss.Value; 

                if (!(position.StopLoss.HasValue) || (newStopLoss.round(this) - position.StopLoss) * _factor > 0)
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
        /// return a a signal of type SignaType in synchronisation with the signal indicator.
		/// 
		/// 'Count-1' is the index of the last candle so 'Count-2' is the preview candle
		/// You cannot use 'Count-1' because the Close property is not defined for the last 
		/// active candle, but CandleStickTendency use 
        /// </summary>
        /// <returns></returns>
        private SignalType determineSignal()
        {
			int index = MarketSeries.Close.Count - 1;
			SignalType signalType = SignalType.Neutral;

			int period = 2 * (int) (GlobalTimeFrame.ToTimeSpan().Ticks / MarketSeries.TimeFrame.ToTimeSpan().Ticks);

			double volatility = _signalIndicator.LocalMA.volatility(period);

			bool isGlobalUp = _signalIndicator.GlobalTrendSignal[index]> 0;
			bool isLocalUp = _signalIndicator.LocalTrendSignal[index] > 0;
			bool isLocalMAOverThreshold = _signalIndicator.LocalMA[index] > GlobalCeilCoefficient * volatility / 2;
			bool isGlobalDown = _signalIndicator.GlobalTrendSignal[index] < 0;
			bool isLocalDown = _signalIndicator.LocalTrendSignal[index] < 0;
			bool isLocalMABelowThreshold = _signalIndicator.LocalMA[index] < -GlobalCeilCoefficient * volatility/2;

			if (!isGlobalUp && !isGlobalDown && !isLocalUp && !isLocalDown)
				signalType = SignalType.Neutral;

			if(!isGlobalUp && !isGlobalDown)
				signalType = SignalType.StrongNeutral;

			if(!isLocalUp && !isLocalDown)
				signalType = SignalType.WeakNeutral;

			if(isLocalUp)
				signalType = SignalType.WeakBuy;
			else
				if(isLocalDown)
					signalType = SignalType.WeakSell;	

			if(isLocalMABelowThreshold)
				signalType = SignalType.StrongBuy;
			else
				if (isLocalMAOverThreshold)
					signalType = SignalType.StrongSell;
			

			return signalType;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalType"></param>
		/// <returns></returns>
		private bool isStrongSignal(SignalType signalType)
		{
			return (signalType == SignalType.StrongBuy || signalType == SignalType.StrongSell);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalType"></param>
		/// <returns></returns>
		private bool isWeakSignal(SignalType signalType)
		{
			return (signalType == SignalType.WeakBuy || signalType == SignalType.WeakSell);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalType"></param>
		/// <returns></returns>
		private bool isBuySignal(SignalType signalType)
		{
			return (signalType == SignalType.StrongBuy || signalType == SignalType.WeakBuy);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalType"></param>
		/// <returns></returns>
		private bool isSellSignal(SignalType signalType)
		{
			return (signalType == SignalType.StrongSell || signalType == SignalType.WeakSell);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalType"></param>
		/// <returns></returns>
		private bool isNeutralSignal(SignalType signalType)
		{
			return (signalType == SignalType.StrongNeutral || signalType == SignalType.WeakNeutral || signalType == SignalType.Neutral);
		}

		/// <summary>
		/// Return the type Buy or Sell or null in correspondance with Signal type.
		/// </summary>
		/// <returns></returns>
		private TradeType? tradeType(SignalType signalType)
		{
			TradeType? tradeType = null;

			if(signalType == SignalType.StrongBuy || signalType == SignalType.WeakBuy)
				tradeType = TradeType.Buy;
			else
				if(signalType == SignalType.StrongSell || signalType == SignalType.WeakSell)
					tradeType = TradeType.Sell;
				else
					tradeType = null;

			return tradeType;
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
				"\nNext Volume\t: " + Math.Round(Volume / LotSize /*Symbol.LotSize*/, 2) + " lots";

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
					double potentialLoss = Math.Round(position.potentialLoss().Value);
					double pipsToStopLoss = Math.Round(position.stopLossToPips(Symbol).Value, 2);

					positionsInfos.AppendFormat(format, position.Id, potentialLoss, pipsToStopLoss);				
				}

            }

            ChartObjects.DrawText("positionsInfos", positionsInfos.ToString(), StaticPosition.TopRight);
        }

        /// <summary>
        /// Calculate the average price of the entry price position. 
		/// It's a barycentre of price ponderate by the corresponding volumes position.
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

			bool isTimeToTrade = (Server.Time.TimeOfDay >= _startTime.TimeOfDay) && (Server.Time.TimeOfDay <= _endTime.TimeOfDay || _endTime.Day == _startTime.Day+1);

			if(!isTimeToTrade)
			{
				Print("Start time : {0}, server time : {1}, finish time : {2}", _startTime.TimeOfDay, Server.Time.TimeOfDay, _endTime.TimeOfDay);
				return false;
			}
			else
				return true;
			

		}
    }
}
