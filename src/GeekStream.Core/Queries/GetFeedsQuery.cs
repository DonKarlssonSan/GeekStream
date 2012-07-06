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
    public class GetFeedsQuery : Query<GeekStreamModel,FeedView[]>
    {
    	int _take;
    	SortMode _sortMode;

    	public GetFeedsQuery(int take,SortMode sortMode)
    	{
    		_take = take;
    		_sortMode = sortMode;
    	}

        #region Overrides of Query<GeekStreamModel,FeedView[]>

        protected override FeedView[] Execute(GeekStreamModel m)
        {
            var feeds = m.PopularFeeds().ToArray();

            return feeds.Take(_take).Select(f => new FeedView(f)).ToArray();
        }

        #endregion
    }
}