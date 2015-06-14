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

// Project Hosting for Open Source Software on Github : 
#endregion


using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
	[Robot("Robot Forex", AccessRights = AccessRights.None)]
	public class Robot_Forex_SL : Robot
	{
		[Parameter(DefaultValue = 10000, MinValue = 1000)]
		public int FirstLot
		{
			get;
			set;
		}

		[Parameter(DefaultValue = 10000, MinValue = 1000)]
		public int LotStep
		{
			get;
			set;
		}

		//[Parameter(DefaultValue = 300)]
		//public int PipStep { get; set; }

		[Parameter("Stop_Loss", DefaultValue = 50, MinValue = 0)]
		public int Stop_Loss
		{
			get;
			set;
		}

		[Parameter("Take_Profit", DefaultValue = 180, MinValue = 10)]
		public int TakeProfit
		{
			get;
			set;
		}

		[Parameter("Tral_Start", DefaultValue = 50, MinValue = 10)]
		public int Tral_Start
		{
			get;
			set;
		}

		[Parameter("Tral_Stop", DefaultValue = 50, MinValue = 10)]
		public int Tral_Stop
		{
			get;
			set;
		}




		[Parameter(DefaultValue = 5, MinValue = 2)]
		public int MaxOrders
		{
			get;
			set;
		}

		private Position position;
		private bool RobotStopped;
		private string botLabel;


		protected override void OnStart()
		{
			botLabel = ToString();

			// The stop loss must be greater than tral stop
			//Stop_Loss = Math.Max(Tral_Stop, Stop_Loss);

			Positions.Opened += OnPositionOpened;
		}

		protected override void OnTick()
		{
			double Bid = Symbol.Bid;
			double Ask = Symbol.Ask;
			double Point = Symbol.TickSize;

			if(Trade.IsExecuting)
				return;

			Position[] positions = Positions.FindAll(botLabel, Symbol);

			if(positions.Length > 0 && RobotStopped)
				return;
			else
				RobotStopped = false;

			if(positions.Length == 0)
				SendFirstOrder(FirstLot);
			else
				ControlSeries();

			foreach(var position in positions)
			{
				if(position.SymbolCode == Symbol.Code)
				{

					if(position.TradeType == TradeType.Buy)
					{
						if(Bid - GetAveragePrice(TradeType.Buy) >= Tral_Start * Point)
							if(Bid - Tral_Stop * Point >= position.StopLoss)
								ModifyPosition(position, Bid - Tral_Stop * Point, position.TakeProfit);
					}

					if(position.TradeType == TradeType.Sell)
					{
						if(GetAveragePrice(TradeType.Sell) - Ask >= Tral_Start * Point)
							if(Ask + Tral_Stop * Point <= position.StopLoss || position.StopLoss == 0)
								ModifyPosition(position, Ask + Tral_Stop * Point, position.TakeProfit);
					}
				}
			}
		}

		protected override void OnError(Error CodeOfError)
		{
			if(CodeOfError.Code == ErrorCode.NoMoney)
			{
				RobotStopped = true;
				Print("ERROR!!! No money for order open, robot is stopped!");
			}
			else if(CodeOfError.Code == ErrorCode.BadVolume)
			{
				RobotStopped = true;
				Print("ERROR!!! Bad volume for order open, robot is stopped!");
			}
		}

		private void SendFirstOrder(int OrderVolume)
		{
			switch(GetStdIlanSignal())
			{
				case 0:
					ExecuteMarketOrder(TradeType.Buy, Symbol, OrderVolume, botLabel);
					break;
				case 1:
					ExecuteMarketOrder(TradeType.Sell, Symbol, OrderVolume, botLabel);
					break;
			}
		}

		private void OnPositionOpened(PositionOpenedEventArgs args)
		{
			double? StopLossPrice = null;
			double? TakeProfitPrice = null;

			Position[] positions = Positions.FindAll(botLabel, Symbol);

			if(positions.Length == 1)
			{
				position = args.Position;

				if(position.TradeType == TradeType.Buy)
					TakeProfitPrice = position.EntryPrice + TakeProfit * Symbol.TickSize;
				if(position.TradeType == TradeType.Sell)
					TakeProfitPrice = position.EntryPrice - TakeProfit * Symbol.TickSize;
			}
			else
				switch(GetPositionsSide())
				{
					case 0:
						TakeProfitPrice = GetAveragePrice(TradeType.Buy) + TakeProfit * Symbol.TickSize;
						break;
					case 1:
						TakeProfitPrice = GetAveragePrice(TradeType.Sell) - TakeProfit * Symbol.TickSize;
						break;
				}

			foreach(Position position in positions)
			{
				if(StopLossPrice != null || TakeProfitPrice != null)
					ModifyPosition(position, position.StopLoss, TakeProfitPrice);
			}
		}

		private double GetAveragePrice(TradeType TypeOfTrade)
		{
			double Result = Symbol.Bid;
			double AveragePrice = 0;
			long count = 0;

			foreach(Position position in Positions.FindAll(botLabel, Symbol))
			{
				if(position.TradeType == TypeOfTrade)
				{
					AveragePrice += position.EntryPrice * position.Volume;
					count += position.Volume;
				}
			}

			if(AveragePrice > 0 && count > 0)
				Result = AveragePrice / count;

			return Result;
		}

		private int GetPositionsSide()
		{
			int Result = -1;
			int BuySide = 0, SellSide = 0;

			foreach(Position position in Positions.FindAll(botLabel, Symbol))
			{
				if(position.TradeType == TradeType.Buy)
					BuySide++;

				if(position.TradeType == TradeType.Sell)
					SellSide++;
			}

			if(BuySide == Positions.Count)
				Result = 0;

			if(SellSide == Positions.Count)
				Result = 1;

			return Result;
		}

		/// <summary>
		/// The gradient variable is a dynamic value that represente an equidistant grid between
		/// the high value and the low value of price.
		/// </summary>
		/// 
		private void ControlSeries()
		{
			const int BarCount = 25;
			int gradient = MaxOrders - 1;

			foreach(Position position in Positions.FindAll(botLabel, Symbol))
			{
				if(-position.Pips > Stop_Loss)
					ClosePosition(position);

			}

			//if (PipStep == 0)
			int _pipstep = GetDynamicPipstep(BarCount, gradient);
			//else
			//	_pipstep = PipStep;

			if(Positions.Count < MaxOrders)
			{
				//int rem;
				long NewVolume = Symbol.NormalizeVolume(FirstLot + FirstLot * Positions.FindAll(botLabel, Symbol).Length, RoundingMode.ToNearest);
				int positionSide = GetPositionsSide();

				switch(positionSide)
				{
					case 0:
						if(Symbol.Ask < FindLastPrice(TradeType.Buy) - _pipstep * Symbol.TickSize)
						{
							//NewVolume = Math.DivRem((int)(FirstLot + FirstLot * Positions.Count), LotStep, out rem) * LotStep;

							if(NewVolume >= LotStep)
								ExecuteMarketOrder(TradeType.Buy, Symbol, NewVolume, botLabel);
						}
						break;

					case 1:
						if(Symbol.Bid > FindLastPrice(TradeType.Sell) + _pipstep * Symbol.TickSize)
						{
							//NewVolume = Math.DivRem((int)(FirstLot + FirstLot * Positions.Count), LotStep, out rem) * LotStep;

							if(NewVolume >= LotStep)
								ExecuteMarketOrder(TradeType.Sell, Symbol, NewVolume, botLabel);
						}
						break;
				}
			}

		}

		private int GetDynamicPipstep(int CountOfBars, int gradient)
		{
			int Result;
			double HighestPrice = 0, LowestPrice = 0;
			int StartBar = MarketSeries.Close.Count - 2 - CountOfBars;
			int EndBar = MarketSeries.Close.Count - 2;

			for(int i = StartBar; i < EndBar; i++)
			{
				if(HighestPrice == 0 && LowestPrice == 0)
				{
					HighestPrice = MarketSeries.High[i];
					LowestPrice = MarketSeries.Low[i];
					continue;
				}

				if(MarketSeries.High[i] > HighestPrice)
					HighestPrice = MarketSeries.High[i];

				if(MarketSeries.Low[i] < LowestPrice)
					LowestPrice = MarketSeries.Low[i];
			}

			Result = (int)((HighestPrice - LowestPrice) / Symbol.TickSize / gradient);

			return Result;
		}

		private double FindLastPrice(TradeType TypeOfTrade)
		{
			double LastPrice = 0;

			foreach(Position position in Positions.FindAll(botLabel, Symbol))
			{
				if(TypeOfTrade == TradeType.Buy)
					if(position.TradeType == TypeOfTrade)
					{
						if(LastPrice == 0)
						{
							LastPrice = position.EntryPrice;
							continue;
						}
						if(position.EntryPrice < LastPrice)
							LastPrice = position.EntryPrice;
					}

				if(TypeOfTrade == TradeType.Sell)
					if(position.TradeType == TypeOfTrade)
					{
						if(LastPrice == 0)
						{
							LastPrice = position.EntryPrice;
							continue;
						}
						if(position.EntryPrice > LastPrice)
							LastPrice = position.EntryPrice;
					}
			}

			return LastPrice;
		}

		private int GetStdIlanSignal()
		{
			int Result = -1;
			int LastBarIndex = MarketSeries.Close.Count - 2;
			int PrevBarIndex = LastBarIndex - 1;

			if(MarketSeries.Close[LastBarIndex] > MarketSeries.Open[LastBarIndex])
				if(MarketSeries.Close[PrevBarIndex] > MarketSeries.Open[PrevBarIndex])
					Result = 0;

			if(MarketSeries.Close[LastBarIndex] < MarketSeries.Open[LastBarIndex])
				if(MarketSeries.Close[PrevBarIndex] < MarketSeries.Open[PrevBarIndex])
					Result = 1;

			return Result;
		}
	}
}
