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
        [Parameter("Martingale On Gain", DefaultValue = true)]
        public bool MartingaleGain { get; set; }

        [Parameter("Martingale", DefaultValue = 0.5, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Martingale Base Grid Step", DefaultValue = 7.5, MinValue = 3)]
        public double MartingaleBaseGridStep { get; set; }

        [Parameter("Money Management (%)", DefaultValue = 1.6, MinValue = 0)]
        public double MoneyManagement { get; set; }

        [Parameter("Stop Loss", DefaultValue = 27.5, MinValue = 0.5)]
        public double StopLoss { get; set; }

        [Parameter("Max Orders", DefaultValue = 2, MinValue = 2)]
        public int MaxOrders { get; set; }

        [Parameter("WPR Signal", DefaultValue=false)]
        public bool WprSignal { get; set; }

        [Parameter("WPR Source")]
        public DataSeries WprSource { get; set; }

        [Parameter("WPR Period", DefaultValue = 17, MinValue = 1)]
        public int WprPeriod { get; set; }

        [Parameter("WPR Overbuy Ceil", DefaultValue = -20, MinValue = -100, MaxValue = 0)]
        public int WprOverbuyCeil { get; set; }

        [Parameter("WPR Oversell Ceil", DefaultValue = -80, MinValue = -100, MaxValue = 0)]
        public int WprOversellCeil { get; set; }

        [Parameter("WPR Crossed Period", DefaultValue = 2, MinValue = 0)]
        public int WprCrossedPeriod { get; set; }

        [Parameter("WPR Min/Max Period", DefaultValue = 114)]
        public int WprMinMaxPeriod { get; set; }

        [Parameter("WPR Exceed MinMax", DefaultValue = 2)]
        public int WprExceedMinMax { get; set; }

        [Parameter("Double Candle Signal", DefaultValue=true)]
        public bool DoubleCandleSignal { get; set; }

        [Parameter("Bollinger Divisions", DefaultValue=8)]
		public int BollingerDivisions { get; set; }
		


        #endregion

        #region cBot variables

        private string _botName;
        private string _botVersion = Assembly.GetExecutingAssembly().FullName.Split(',')[1].Replace("Version=", "").Trim();

        // le label permet de s'y retrouver parmis toutes les positions.
        private string _instanceLabel;

        // Est une suite d'achat (Buy) ou une suite de vente (Sell).
        private TradeType? _tradesType = null;

        // premier nextVolume utilisé.
        private double _firstVolume;
        List<Strategy> _strategies;
        private StaticPosition _cornerPosition;
        private bool _debug;
        private int _nPositions;
		bool isControlSeries;

        private double _firstEntryPrice;
        private double _lastEntryPrice;
        private double _previewEntryPrice;

        #endregion

        #region cBot Events

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
			if (DoubleCandleSignal)
				_strategies.Add(new DoubleCandleStrategy(this, 14, 0, BollingerDivisions));
            
			if(WprSignal)
				_strategies.Add(new WPRSStrategy(this, WprSource, WprPeriod, WprOverbuyCeil, WprOversellCeil, WprCrossedPeriod, WprMinMaxPeriod, WprExceedMinMax));

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
                Print("The current symbol is {0}", Symbol.Code);
                Print("The current symbol has PipSize (deposit currency) of: {0}", Symbol.PipSize);
                Print("The current symbol has PipValue (quote currency) of: {0}", Symbol.PipValue);
                Print("The current symbol has TickSize (deposit currency): {0}", Symbol.TickSize);
                Print("The current symbol has TickSValue (quote currency): {0}", Symbol.TickValue);
                Print("The current symbol has {0} Digits", Symbol.Digits);
                Print("The current symbol minimum nextVolume is {0}", Symbol.VolumeMin);
                Print("The current symbol maximum nextVolume is {0}", Symbol.VolumeMax);
                Print("The current symbol step nextVolume is {0}", Symbol.VolumeStep);

            }
        }

        /// <summary>
        ///  Méthode de callback sur chaque tick
        /// </summary>
        protected override void OnTick()
        {
            if (isControlSeries)
                return;
			
			isControlSeries = true;

            ControlSeries();

			isControlSeries = false;
        }


        protected override void OnError(Error error)
        {
            string errorString = this.errorString(error);

            if (errorString != "")
                Print(errorString);
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            _nPositions++;
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            _nPositions--;
        }
        #endregion


        /// <summary>
        /// Gère le control des différentes prises de positions. un seul type de position est pris, le nextVolume des 
        /// positions successives augmentent selon le coefficient de martingale de manière linéaire en cas de pertes 
		/// (MartingaleGain=false). Les nouvelles positions sont prises selon la grille _gridStep dont la 
		/// valeur dépends de la volatilité. Lors d'une nouvelle position dans le cas d'une martingale sur les gains, 
		/// les stops loss des positions précédentes sont ramenées au prix d'achat ou de vente afin d'assurer des stops 
		/// zéro, puis déplacés au prochain prix d'achat ou de vente suivant si la position continue d'être gagnante.
        /// </summary>
        private void ControlSeries()
        {	
			if(_nPositions >= MaxOrders)
				return;

			double gridStep = MartingaleBaseGridStep * Symbol.PipSize * (MarketSeries.volatility(11)/MarketSeries.volatility(33));

			// Calcule le volume en fonction du money management pour un risque maximum et un stop loss donné.
			// Ne tient pas compte des risques sur d'autres positions ouvertes du compte de trading utilisé
			double maxVolume = this.moneyManagement(MoneyManagement, StopLoss);

			if(MartingaleGain)
				_firstVolume = maxVolume;
			else
				_firstVolume = maxVolume / (MaxOrders + (MartingaleCoeff * MaxOrders * (MaxOrders - 1)) / 2.0);

			if (_nPositions==0)
			{
				_tradesType = this.signal(_strategies);

				if(_tradesType.HasValue)
				{
					TradeResult tradeResult = executeOrder(_firstVolume, gridStep);
					_firstEntryPrice = tradeResult.Position.EntryPrice.round(this);
				}
			}
			else
			{
				double nextVolume = _firstVolume * (1 + MartingaleCoeff * _nPositions * (MartingaleGain ? 0:1));

				if (MartingaleGain)
				{
					double diffBetweenPriceAndLastEntryPrice = ((_tradesType.isBuy() ? Symbol.Ask : Symbol.Bid) - _lastEntryPrice) * _tradesType.factor();
				
					if ( diffBetweenPriceAndLastEntryPrice >= gridStep)
						executeOrder(nextVolume,gridStep);
				}
				else
					if (((_tradesType.isBuy() ? Symbol.Ask : Symbol.Bid) - _firstEntryPrice) * _tradesType.factor() + gridStep <= 0)
						executeOrder(nextVolume,gridStep);

				if (MartingaleGain)
					foreach (Position position in Positions.FindAll(_instanceLabel, Symbol))
					{
						int factor = position.factor();

						bool isPriceOverPositionPrice = ((position.isBuy() ? Symbol.Bid : Symbol.Ask) - position.EntryPrice) * factor  >= 0;
						bool islastEntryPriceOverPositionPrice = (_lastEntryPrice - position.EntryPrice) * factor > 0;

						if (isPriceOverPositionPrice  && islastEntryPriceOverPositionPrice)
						{
							double newStopLoss = ((position.isBuy() ? Symbol.Bid : Symbol.Ask)).round(this);

							if ((newStopLoss - position.StopLoss)*factor > 0)
								modifyOrder(position, newStopLoss, position.TakeProfit);						}
					}
			}
        }

        /// <summary>
        /// Execute un ordre de type _tradeType. Affiche le prochain niveau de martingale.
        /// </summary>
        /// <param name="nextVolume"></param>
        /// <returns></returns>
        private TradeResult executeOrder(double volume, double gridStep)
        {
            if (!(_tradesType.HasValue) || volume <= 0)
                return null;

            Position pos = Positions.Find(_instanceLabel);
            string comment = string.Format("{0} v{1}", _botName, _botVersion);
            if (pos != null)
                comment = string.Format("{0}-{1}-{2}", comment, pos.Id.ToString(), _nPositions+1);

            long normalizedVolume = Symbol.NormalizeVolume(volume, RoundingMode.ToNearest);

            TradeResult tradeResult = ExecuteMarketOrder(_tradesType.Value, Symbol, normalizedVolume, _instanceLabel, StopLoss, null, 10, comment);

            if (!(tradeResult.IsSuccessful))
                return null;

            _previewEntryPrice = _lastEntryPrice;
			_lastEntryPrice = tradeResult.Position.EntryPrice;
			
            if (MartingaleGain)
                ChartObjects.DrawHorizontalLine("gridLine", _lastEntryPrice + _tradesType.factor() * gridStep, Colors.Navy, 2);
            else // Martingale sur les pertes
            {
				double newStopLoss = (averagePrice() - _tradesType.factor() * StopLoss * Symbol.PipSize).round(this);

				foreach(Position position in Positions.FindAll(_instanceLabel, Symbol))
					if((newStopLoss - position.StopLoss) * position.factor() > 0)
						modifyOrder(position, newStopLoss, null);

                ChartObjects.DrawHorizontalLine("gridLine", _firstEntryPrice - _tradesType.factor() * gridStep, Colors.Navy, 2);
            }

            return tradeResult;
        }

		/// <summary>
		/// Modifie la position si aucune opération est en cours
		/// </summary>
		/// <param name="position"></param>
		/// <param name="stopLoss"></param>
		/// <param name="takeProfit"></param>
		/// <returns></returns>
		private TradeResult modifyOrder(Position position, double? stopLoss, double? takeProfit)
		{
			TradeResult tradeResult = ModifyPosition(position, stopLoss, takeProfit);

			return tradeResult;
		}

        /// <summary>
        /// Calcule la moyenne des prises de positions (prix moyen), c'est un barycentre des 
        /// prix pondéré par les volumes correspondants.
        /// </summary>
        /// <returns></returns>
        private double averagePrice()
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
                throw new System.ArgumentException("averagePrice() : There is no open position");
        }
    }
}
