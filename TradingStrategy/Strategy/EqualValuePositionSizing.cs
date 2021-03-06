﻿using System;
using System.Collections.Generic;
using TradingStrategy.Base;
using TradingStrategy;

namespace TradingStrategy.Strategy
{
    public sealed class EqualValuePositionSizing : GeneralPositionSizingBase
    {
        [Parameter(10, "权益被分割的块数，每份头寸将占有一份. 0表示自适应划分")]
        public int PartsOfEquity { get; set; }

        [Parameter(100, "自适应划分中所允许的最大划分数目, 0表示和MinPartsOfAdpativeAllocation保持一致")]
        public int MaxPartsOfAdpativeAllocation { get; set; }

        [Parameter(1, "自适应划分中所允许的最小划分数目")]
        public int MinPartsOfAdpativeAllocation { get; set; }

        [Parameter(1000, "最大待处理头寸数目，当待处理头寸数目超过此数时不买入任何头寸")]
        public int MaxObjectNumberToBeEstimated { get; set; }

        [Parameter(EquityEvaluationMethod.InitialEquity, "权益计算方法。0:核心权益法,1:总权益法,2:抵减总权益法,3:初始权益法,4:控制损失初始权益法,5:控制损失总权益法,6:控制损失抵减总权益法")]
        public EquityEvaluationMethod EquityEvaluationMethod { get; set; }

        [Parameter(1.0, "权益利用率[0.0..1.0], 0.0代表恒定权益利用率为1.0")]
        public double EquityUtilization { get; set; }

        [Parameter(true, "限制新头寸数目不超过划分数目")]
        public bool LimitNewPositionCountAsParts { get; set; }

        [Parameter(2.0, "对数底, 用于计算权益使用率, 0.0表示不使用")]
        public double LogarithmBase { get; set; }
        
        public override string Name
        {
            get { return "价格等值模型"; }
        }

        public override string Description
        {
            get { return "每份头寸占有的价值是总权益的固定比例(1/PartsOfEquity)"; }
        }

        private BoardIndexBasedEquityUtilizationCalculator _calculator = null;

        private double _latestHighEquity = 0.0;
        private double _dynamicEquityUtilization = 0.0;


        public override void Initialize(IEvaluationContext context, IDictionary<ParameterAttribute, object> parameterValues)
        {
            base.Initialize(context, parameterValues);

            _calculator = new BoardIndexBasedEquityUtilizationCalculator(context);
        }

        protected override void ValidateParameterValues()
        {
            base.ValidateParameterValues();

            if (PartsOfEquity < 0)
            {
                throw new ArgumentOutOfRangeException("PartsOfEquity must be greater than or equal to 0");
            }

            if (EquityUtilization < 0.0 || EquityUtilization > 1.0)
            {
                throw new ArgumentException("EquityUtilization must be in [0.0..1.0]");
            }

            if (LogarithmBase != 0.0 && LogarithmBase <= 1.0)
            {
                throw new ArgumentException("LogarithmBase must be greater than 1.0");
            }
        }

        private double GetEquityUtilizationPenalty(double drawdown)
        {
            drawdown = Math.Abs(drawdown);

            return 2.0 * drawdown;
        }

        private void UpdateDynamicEquityUtilization()
        {
            double initialEquity = Context.GetInitialEquity();

            double currentEquity = Context.GetCurrentEquity(CurrentPeriod, EquityEvaluationMethod.TotalEquity);

            if (currentEquity > _latestHighEquity)
            {
                _latestHighEquity = currentEquity;
            }

            double drawdown = Math.Abs((currentEquity - _latestHighEquity) / _latestHighEquity);

            _dynamicEquityUtilization = EquityUtilization * (1.0 - GetEquityUtilizationPenalty(drawdown));

            if (_dynamicEquityUtilization < 0.1)
            {
                _dynamicEquityUtilization = 0.1;
            }
        }

        private double GetDynamicEquityUtilization(ITradingObject tradingObject)
        {
            if(EquityUtilization == 0.0)
            {
                return 1.0;
            }

            return _dynamicEquityUtilization;
        }

        public override void StartPeriod(DateTime time)
        {
            base.StartPeriod(time);

            UpdateDynamicEquityUtilization();
        }

        private int GetParts(int totalNumberOfObjectsToBeEstimated)
        {
            var maxParts = MaxPartsOfAdpativeAllocation == 0 ? MinPartsOfAdpativeAllocation : MaxPartsOfAdpativeAllocation;

            int parts = PartsOfEquity == 0
                ? Math.Max(Math.Min(totalNumberOfObjectsToBeEstimated, maxParts), MinPartsOfAdpativeAllocation)
                : PartsOfEquity;

            if (LogarithmBase != 0.0
                && EquityEvaluationMethod != TradingStrategy.EquityEvaluationMethod.InitialEquity)
            {
                double currentEquity = Context.GetCurrentEquity(CurrentPeriod, EquityEvaluationMethod);
                double initialEquity = Context.GetInitialEquity();
                double proportion = (1.0 + Math.Log(currentEquity / initialEquity, LogarithmBase));

                if (proportion < 0.0)
                {
                    proportion = 1.0;
                }

                parts = Math.Max(parts, (int)(parts * proportion));
            }

            return parts;
        }

        public override int GetMaxPositionCount(int totalNumberOfObjectsToBeEstimated)
        {
            if (LimitNewPositionCountAsParts)
            {
                return GetParts(totalNumberOfObjectsToBeEstimated);
            }
            else
            {
                return base.GetMaxPositionCount(totalNumberOfObjectsToBeEstimated);
            }
        }

        public override PositionSizingComponentResult EstimatePositionSize(ITradingObject tradingObject, double price, double stopLossGap, int totalNumberOfObjectsToBeEstimated)
        {
            var result = new PositionSizingComponentResult();

            if (totalNumberOfObjectsToBeEstimated > MaxObjectNumberToBeEstimated)
            {
                return result;
            }

            var currentEquity = Context.GetCurrentEquity(CurrentPeriod, EquityEvaluationMethod);

            int parts = GetParts(totalNumberOfObjectsToBeEstimated);

            double totalEquityUtilization = GetDynamicEquityUtilization(tradingObject);

            double boardIndexUtilization = _calculator.CalculateEquityUtilization(tradingObject);

            double finalUtilization = totalEquityUtilization * boardIndexUtilization;

            result.Comments = string.Format(
                "positionsize = currentEquity({0:0.000}) * equityUtilization({1:0.000}) / Parts ({2}) / price({3:0.000})",
                currentEquity,
                finalUtilization,
                parts,
                price);

            result.PositionSize = (int)(currentEquity * finalUtilization/ parts / price);

            return result;
        }
    }
}
