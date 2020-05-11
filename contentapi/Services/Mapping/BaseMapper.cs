using System;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Mapping
{
    /// <summary>
    /// The most basic mapper: everything has an ID and a create date
    /// </summary>
    public class BaseMapper
    {
        public void ApplyToViewBasic(EntityBase entityBase, BaseView view)
        {
            view.id = entityBase.id;
            view.createDate = (DateTime)entityBase.createDateProper();
        }

        public void ApplyFromViewBasic(BaseView view, EntityBase entityBase)
        {
            entityBase.id = view.id;
            entityBase.createDate = view.createDate;
        }
    }
}