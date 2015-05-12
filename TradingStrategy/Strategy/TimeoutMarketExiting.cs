﻿using System;
using System.Collections.Generic;
using System.Linq;
using StockAnalysis.Share;
using TradingStrategy.Base;

namespace TradingStrategy.Strategy
{
    public sealed class TimeoutMarketExiting 
        : GeneralMarketExitingBase
    {
        private readonly PeriodCounter<DateTime> _periodCounter = new PeriodCounter<DateTime>();

        public override string Name
        {
            get { return "定时退出"; }
        }

        public override string Description
        {
            get { return "当头寸持有超过一段时间后立即退出市场"; }
        }

        [Parameter(20, "头寸持有周期数")]
        public int HoldingPeriods { get; set; }

        protected override void ValidateParameterValues()
        {
 	        base.ValidateParameterValues();

            if (HoldingPeriods < 0)
            {
                throw new ArgumentOutOfRangeException("HoldingPeriods must be great than 0");
            }
        }

        public override void EvaluateSingleObject(ITradingObject tradingObject, Bar bar)
        {
            var code = tradingObject.Code;
            if (Context.ExistsPosition(code))
            {
                var latestBuyTime = Context.GetPositionDetails(code).Max(p => p.BuyTime);

                if (!_periodCounter.Exists(tradingObject))
                {
                    var periodCount = latestBuyTime < CurrentPeriod ? 1 : 0;

                    _periodCounter.InitializeOrUpdate(tradingObject, periodCount, latestBuyTime);
                }
                else
                {
                    DateTime prevPositionLatestBuyTime;
                    _periodCounter.GetPeriod(tradingObject, out prevPositionLatestBuyTime);

                    if (latestBuyTime > prevPositionLatestBuyTime)
                    {
                        // new postion has been created, we need to reset record
                        var periodCount = latestBuyTime < CurrentPeriod ? 1 : 0;

                        _periodCounter.Remove(tradingObject);
                        _periodCounter.InitializeOrUpdate(tradingObject, periodCount, latestBuyTime);

                    }
                    else
                    {
                        // just update period
                        _periodCounter.InitializeOrUpdate(tradingObject, 0);
                    }
                }
            }
            else
            {
                _periodCounter.Remove(tradingObject);
            }
        }

        public override bool ShouldExit(ITradingObject tradingObject, out string comments)
        {
            comments = string.Empty;

            int periodCount = _periodCounter.GetPeriod(tradingObject);

            if (periodCount >= HoldingPeriods)
            {
                comments = string.Format("hold for {0} periods", HoldingPeriods);
                return true;
            }

            return false;
        }
    }
}
