using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class BaseViewSource
    {
        protected IMapper mapper;
        protected ILogger logger;
        protected IEntityProvider provider;

        public BaseViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider)
        {
            this.logger = logger;
            this.mapper = mapper;
            this.provider = provider;
        }

        /// <summary>
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        public virtual E LimitSearch<E>(E search) where E : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        public IQueryable<E> Q<E>() where E : EntityBase
        {
            return provider.GetQueryable<E>();
        }

        /// <summary>
        /// Modify the given query such that only those with the given parents are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parentIds"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<E> LimitByParents<E>(IQueryable<E> query, List<long> parentIds) where E : EntityGroup
        {
            return  from q in query
                    join r in Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where r.type == Keys.ParentRelation && parentIds.Contains(r.entityId1)
                    select q;
        }

        /// <summary>
        /// Modify the given query such that only those with matching values are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="keyLike"></param>
        /// <param name="valueLike"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<E> LimitByValue<E>(IQueryable<E> query, string keyLike, string valueLike) where E : EntityGroup
        {
            return  from q in query
                    join v in Q<EntityValue>() on q.entity.id equals v.entityId
                    where EF.Functions.Like(v.key, keyLike) && EF.Functions.Like(v.value, valueLike)
                    select q;
        }

        public IQueryable<E> GetByIds<E>(IQueryable<long> ids) where E : EntityBase
        {
            return
                from e in provider.GetQueryable<E>()
                join i in ids on e.id equals i
                select e;
        }

        public virtual IQueryable<long> FinalizeQuery<E,S>(IQueryable<E> query, S search, Expression<Func<E,long>> mainIdSelector) where S : EntitySearchBase
        {
            var husks = query.GroupBy(mainIdSelector).Select(x => new EntityBase() { id = x.Key });

            //Note: applyfinal finalizes some limiters (such as skip/take) and ALSO tries to apply
            //the fallback ordering. This is ID and random, which we don't need to implement up here.
            return provider.ApplyFinal(husks, search).Select(x => x.id);
        }
    }
}