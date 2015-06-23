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
// Pour résumer c'est une martingale avec stop loss et money management.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Lib;

namespace cAlgo.Robots
{
    [Robot("Martingale Forex", AccessRights = AccessRights.None)]
    public class Martingale_Forex : Robot
    {
        #region Parameters
        [Parameter("Martingale Buy", DefaultValue = true)]
        public bool MartingaleBuy { get; set; }

        [Parameter("Money Management (%)", DefaultValue = 1.6, MinValue = 0)]
        public double MoneyManagement { get; set; }

        [Parameter("Take Profit", DefaultValue = 5, MinValue = 5)]
        public double TakeProfit { get; set; }

        [Parameter("Stop Loss", DefaultValue = 27.5, MinValue = 0.5)]
        public double StopLoss { get; set; }

        [Parameter("Martingale", DefaultValue = 0.5, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Max Orders", DefaultValue = 2, MinValue = 2)]
        public int MaxOrders { get; set; }

        #endregion

        private bool _isRobotStopped;
        private string _botName;
        private string _botVersion = "1.3.4.0";
		private string _instanceLabel;					// le label permet de s'y retrouver parmis toutes les instances possibles.
		private TradeType? _tradesType = null;			// Est une suite d'achat (Buy) ou une suite de vente (Sell).
        private double _firstVolume;					// premier volume utilisé.
        private StaticPosition _cornerPosition;
        private bool _debug;
		private int nPositions;


        protected override void OnStart()
        {
            _debug = false;
			nPositions = 0;
            _botName = ToString();
            _instanceLabel = _botName + "-" + _botVersion + "-" + Symbol.Code + "-" + TimeFrame.ToString();

            Positions.Opened += OnPositionOpened;
			Positions.Closed += OnPositionClosed;

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

			if(_debug)
			{
				Print("The current symbol has PipSize of: {0}", Symbol.PipSize);
				Print("The current symbol has PipValue of: {0}", Symbol.PipValue);
				Print("The current symbol has TickSize: {0}", Symbol.TickSize);
				Print("The current symbol has TickSValue: {0}", Symbol.TickValue);
			}
        }

        protected override void OnTick()
        {
            if (Trade.IsExecuting)
                return;

            Position[] positions = GetPositions();

            if (positions.Length > 0 && _isRobotStopped)
                return;
            else
                _isRobotStopped = false;

            if (positions.Length == 0)
            {
                // Calcule le volume en fonction du money management pour un risque maximum et un stop loss donné.
                // Ne tient pas compte des risques sur d'autres positions ouvertes du compte de trading utilisé
                double maxVolume = this.moneyManagement(MoneyManagement, StopLoss);
				if(MartingaleBuy)
					_firstVolume = maxVolume;
				else
					_firstVolume = maxVolume / (MaxOrders + (MartingaleCoeff * MaxOrders * (MaxOrders - 1)) / 2.0);

                if (_firstVolume <= 0)
                    throw new System.ArgumentException(String.Format("the 'first lot' : {0} parameter must be positive and not null", _firstVolume));
                else
                    executeFirstOrder(_firstVolume);
            }
            else
                ControlSeries();
        }

        protected override void OnError(Error CodeOfError)
        {
            if (CodeOfError.Code == ErrorCode.NoMoney)
            {
                _isRobotStopped = true;
                Print("ERROR!!! No money for order open, robot is stopped!");
            }
            else if (CodeOfError.Code == ErrorCode.BadVolume)
            {
                _isRobotStopped = true;
                Print("ERROR!!! Bad volume for order open, robot is stopped!");
            }
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
			nPositions++;

            double? StopLossPrice = null;
            double? takeProfitPrice = null;
			double averagePrice = GetAveragePrice();

            switch (GetPositionsSide())
            {
                case 0:
					takeProfitPrice = averagePrice + TakeProfit * Symbol.PipSize;
					StopLossPrice = averagePrice - StopLoss * Symbol.PipSize;
                    break;
                case 1:
					takeProfitPrice = averagePrice - TakeProfit * Symbol.PipSize;
					StopLossPrice = averagePrice + StopLoss * Symbol.PipSize;
                    break;
            }

            if (StopLossPrice.HasValue || takeProfitPrice.HasValue)
            {
                Position[] positions = GetPositions();

                foreach (Position position in positions)
                {
                    if (StopLossPrice != position.StopLoss || takeProfitPrice != position.TakeProfit)
                        ModifyPosition(position, StopLossPrice, takeProfitPrice);
                }
            }
        }

		private void OnPositionClosed(PositionClosedEventArgs args)
		{
			nPositions--;
		}

        private void executeFirstOrder(double OrderVolume)
        {
			_tradesType = GetSignal();
                     
			if (_tradesType.HasValue)
				executeOrder(_tradesType, OrderVolume);
        }

        private void ControlSeries()
        {
			Debug.Assert(nPositions > 0 );

            if (nPositions < MaxOrders)
            {
                long volume = Symbol.NormalizeVolume(_firstVolume * (1 + MartingaleCoeff * nPositions), RoundingMode.ToNearest);
                int countOfBars = (int)(25.0 / nPositions);

                int pipstep = GetDynamicPipstep(countOfBars, MaxOrders+1);

				if (MartingaleBuy)
				{
						double? firstPrice = GetFirstPrice();

						switch(_tradesType)
						{
							case TradeType.Buy:
								ChartObjects.DrawHorizontalLine("gridBuyLine", firstPrice.Value + pipstep * Symbol.PipSize, Colors.Green, 2);

								if(Symbol.Ask >= firstPrice.Value + pipstep * Symbol.PipSize)
									executeOrder(TradeType.Buy, volume);
								break;

							case TradeType.Sell:
								ChartObjects.DrawHorizontalLine("gridSellLine", firstPrice.Value - pipstep * Symbol.PipSize, Colors.Red, 2);

								if(Symbol.Bid <= firstPrice.Value + pipstep * Symbol.PipSize)
									executeOrder(TradeType.Sell, volume);
								break;
						}
				}
				else
				{
						double? lastPrice = GetLastPrice();

							switch(_tradesType)
							{
								case TradeType.Buy:
									ChartObjects.DrawHorizontalLine("gridBuyLine", lastPrice.Value - pipstep * Symbol.PipSize, Colors.Green, 2);

									if(Symbol.Ask <= lastPrice.Value - pipstep * Symbol.PipSize)
										executeOrder(TradeType.Buy, volume);
									break;

								case TradeType.Sell:
									ChartObjects.DrawHorizontalLine("gridSellLine", lastPrice.Value + pipstep * Symbol.PipSize, Colors.Red, 2);

									if(Symbol.Bid >= lastPrice.Value + pipstep * Symbol.PipSize)
										executeOrder(TradeType.Sell, volume);
									break;
							}				
				}



            }

            //if (!DEBUG)
            //	ChartObjects.DrawText("MaxDrawdown", "MaxDrawdown: " + Math.Round(GetMaxDrawdown(), 2) + " Percent", corner_position);
        }

        // You can modify the condition of entry here.
        private TradeType? GetSignal()
        {
            int LastBarIndex = MarketSeries.Close.Count - 2;
            int PrevBarIndex = LastBarIndex - 1;

            // two up candles for a buy signal.
            if (MarketSeries.Close[LastBarIndex] > MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] > MarketSeries.Open[PrevBarIndex])
                    return TradeType.Buy;

            // two down candles for a sell signal.
            if (MarketSeries.Close[LastBarIndex] < MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] < MarketSeries.Open[PrevBarIndex])
                    return TradeType.Sell;

            return null;
        }

        private TradeResult executeOrder(TradeType? tradeType, double volume)
        {
            //Print("normalized volume : {0}", Symbol.NormalizeVolume(volume, RoundingMode.ToNearest));
			if(tradeType.HasValue)
				return ExecuteMarketOrder(tradeType.Value, Symbol, Symbol.NormalizeVolume(volume, RoundingMode.ToNearest), _instanceLabel);
			else
				return null;
        }

        private Position[] GetPositions()
        {
            return Positions.FindAll(_instanceLabel, Symbol);
        }

        private double GetAveragePrice()
        {
            double sum = 0;
            long count = 0;

            foreach (Position position in GetPositions())
            {
				sum += position.EntryPrice * position.Volume;
                count += position.Volume;
            }

			if(sum > 0 && count > 0)
				return  sum / count;
			else
				throw new System.ArgumentException("GetAveragePrice() : There is no open position");
        }

        private int GetPositionsSide()
        {
            int Result = -1;
            int BuySide = 0, SellSide = 0;
            Position[] positions = GetPositions();

            foreach (Position position in positions)
            {
                if (position.TradeType == TradeType.Buy)
                    BuySide++;

                if (position.TradeType == TradeType.Sell)
                    SellSide++;
            }

            if (BuySide == positions.Length)
                Result = 0;

            if (SellSide == positions.Length)
                Result = 1;

            return Result;
        }

        private int GetDynamicPipstep(int CountOfBars, int division)
        {
            double HighestPrice = 0, LowestPrice = 0;
            int StartBar = MarketSeries.Close.Count - 2 - CountOfBars;
            int EndBar = MarketSeries.Close.Count - 2;

            for (int i = StartBar; i < EndBar; i++)
            {

                if (MarketSeries.High[i] > HighestPrice || HighestPrice == 0 )
                    HighestPrice = MarketSeries.High[i];

                if (MarketSeries.Low[i] < LowestPrice || LowestPrice == 0)
                    LowestPrice = MarketSeries.Low[i];
            }

           return (int)((HighestPrice - LowestPrice) / Symbol.PipSize / division);
        }

        private double savedMaxBalance;
        private List<double> drawdown = new List<double>();
        private double GetMaxDrawdown()
        {
            savedMaxBalance = Math.Max(savedMaxBalance, Account.Balance);

            drawdown.Add((savedMaxBalance - Account.Balance) / savedMaxBalance * 100);
            drawdown.Sort();

            double maxDrawdown = drawdown[drawdown.Count - 1];

            return maxDrawdown;
        }

        private double? GetLastPrice()
        {
			double lastPrice = 0;

			if(_tradesType.HasValue)
				foreach(Position position in GetPositions())
				{
					if(_tradesType.isBuy() && (position.EntryPrice < lastPrice || lastPrice == 0))
						lastPrice = position.EntryPrice;
					else
						if(position.EntryPrice > lastPrice || lastPrice == 0)
							lastPrice = position.EntryPrice;
				}
			else
				return null;

			return lastPrice;
        }

		private double? GetFirstPrice()
		{
			double firstPrice = 0;

			if(_tradesType.HasValue)
				foreach(Position position in GetPositions())
				{
					if(_tradesType.isBuy() && (position.EntryPrice > firstPrice || firstPrice == 0))
						firstPrice = position.EntryPrice;
					else 
						if (position.EntryPrice < firstPrice || firstPrice == 0)
							firstPrice = position.EntryPrice;
				}
			else
				return null;

			return firstPrice;
		}


    }
}
