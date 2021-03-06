using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseViewSource<V,T,E,S> : ViewSourceServices, IViewSource<V,T,E,S> 
        where V : IIdView where E : EntityGroup where S : EntitySearchBase, IConstrainedSearcher
    {
        protected IMapper mapper;
        protected ILogger logger;
        protected IEntityProvider provider;

        /// <summary>
        /// Which item in an entity group contains the actual id that represents this group? Is it the relation, the entity, etc?
        /// </summary>
        /// <value></value>
        public abstract Expression<Func<E, long>> MainIdSelector {get;}

        public BaseViewSource(ILogger<BaseViewSource<V,T,E,S>> logger, IMapper mapper, IEntityProvider provider)
        {
            this.logger = logger;
            this.mapper = mapper;
            this.provider = provider;
        }

        public IQueryable<X> Q<X>() where X : EntityBase
        {
            return provider.GetQueryable<X>();
        }

        /// <summary>
        /// Modify the given query such that only those with the given parents are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parentIds"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<E> LimitByParents(IQueryable<E> query, List<long> parentIds)
        {
            return  from q in query
                    join r in Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where r.type == Keys.ParentRelation && parentIds.Contains(r.entityId1)
                    select q;
        }

        public IQueryable<E> GetOrphans(IQueryable<E> query)
        {
            return query.Where(q => !(Q<EntityRelation>().Where(x => x.type == Keys.ParentRelation).Select(x => x.entityId2)).Contains(q.entity.id));
        }

        /// <summary>
        /// Modify the given query such that only those with matching values are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="keyLike"></param>
        /// <param name="valueLike"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<E> LimitByValue(IQueryable<E> query, string keyLike, string valueLike)
        {
            return  from q in query
                    join v in Q<EntityValue>() on q.entity.id equals v.entityId
                    where EF.Functions.Like(v.key, keyLike) && EF.Functions.Like(v.value, valueLike)
                    select q;
        }

        public IQueryable<E> LimitByCreateEdit(IQueryable<E> query, List<long> creators, List<long> editors)
        {
            //Nothing to do, no use joining.
            if(creators.Count == 0 && editors.Count == 0)
                return query;

            var editorStrings = editors.Select(x => x.ToString());

            return  from q in query
                    join r in Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where r.type == Keys.CreatorRelation && 
                        (creators.Count == 0 || creators.Contains(r.entityId1)) && 
                        (editors.Count == 0 || editorStrings.Contains(r.value))
                    select q;
        }

        public IQueryable<X> GetByIds<X>(IQueryable<long> ids) where X : EntityBase
        {
            return
                from e in provider.GetQueryable<X>()
                join i in ids on e.id equals i
                select e;
        }

        public virtual IQueryable<long> FinalizeQuery(IQueryable<E> query, S search)  //where S : EntitySearchBase
        {
            return query.GroupBy(MainIdSelector).Select(x => x.Key);
        }

        private async Task<Dictionary<X, SimpleAggregateData>> GroupAsync<R,X>(IQueryable<long> ids, IQueryable<R> join, Expression<Func<R,X>> keySelector) where R : EntityBase
        {
            var pureList = await provider.GetListAsync(
                ids.Join(join, x => x, r => r.id, (x, r) => r).GroupBy(keySelector).Select(g => new 
                { 
                    key = g.Key, 
                    aggregate = new SimpleAggregateData()
                    {
                        count = g.Count(),
                        lastDate = g.Max(x => x.createDate),
                        firstDate = g.Min(x => x.createDate),
                        lastId = g.Max(x => x.id)
                    }
                })
            );

            pureList.ForEach(x =>
            {
                var kind = DateTime.Now.Kind;

                if(x.aggregate.lastDate != null)
                    x.aggregate.lastDate = new DateTime(((DateTime)x.aggregate.lastDate).Ticks, kind);
                if(x.aggregate.firstDate != null)
                    x.aggregate.firstDate = new DateTime(((DateTime)x.aggregate.firstDate).Ticks, kind);
            });
            
            return pureList.ToDictionary(x => x.key, y => y.aggregate);
        }

        public Task<Dictionary<X, SimpleAggregateData>> GroupAsync<R,X>(IQueryable<long> ids, Expression<Func<R,X>> keySelector) where R : EntityBase
        {
            return GroupAsync(ids, Q<R>(), keySelector);
        }

        /// <summary>
        /// Retrieve the initial objects from the given search (but not finalization such as ordering, limits, etc)
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public abstract IQueryable<E> GetBaseQuery(S search);

        /// <summary>
        /// How to convert a list of database ids into a list of database objects or groups of objects
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public abstract Task<List<T>> RetrieveAsync(IQueryable<long> ids);

        /// <summary>
        /// How to convert the database object or group into the view
        /// </summary>
        /// <param name="basic"></param>
        /// <returns></returns>
        public abstract V ToView(T basic);

        /// <summary>
        /// How to convert the view to a database object or group of objects
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        public abstract T FromView(V view);

        public virtual IQueryable<E> ModifySearch(IQueryable<E> query, S search) { return query; }

        public IQueryable<long> SearchIds(S search, Func<IQueryable<E>, IQueryable<E>> modify = null)
        {
            var query = GetBaseQuery(search);

            query = ModifySearch(query, search);

            if(modify != null)
                query = modify(query);

            //Finalize may include special sorting / etc.
            var husks = FinalizeQuery(query, search).Select(x => new EntityBase() { id = x });

            //Note: applyfinal finalizes some limiters (such as skip/take) and ALSO tries to apply
            //the fallback ordering. This is ID and random, which we don't need to implement up here.
            return provider.ApplyFinal(husks, search).Select(x => x.id);
        }
    }
}