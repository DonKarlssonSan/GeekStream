﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeekStream.Core.Domain;
using GeekStream.Core.Views;
using LiveDomain.Core;

namespace GeekStream.Core.Queries
{
    [Serializable]
    public class GetFeedsToCollectQuery : Query<GeekStreamModel,FeedView[]>
    {
    	int _take, _skip;
        private DateTime _collectedBefore;

        public GetFeedsToCollectQuery(DateTime collectedBefore, int skip = 0, int take = int.MaxValue)
    	{
    		_take = take;
            _skip = skip;
            _collectedBefore = collectedBefore;
    	}


        protected override FeedView[] Execute(GeekStreamModel model)
        {
            return model
                .GetFeedsCollectedBefore(_collectedBefore)
                .OrderByDescending(f => f.Created)
                .Skip(_skip)
                .Take(_take)
                .Select(f => new FeedView(f)).ToArray();
        }
    }
}
