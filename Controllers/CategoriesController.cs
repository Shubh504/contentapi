using System.Collections.Generic;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace contentapi.Controllers
{
    public class CategoriesController : GenericController<Category, CategoryView>
    {
        public CategoriesController(ContentDbContext context, IMapper mapper):base(context, mapper) { }

        public override DbSet<Category> GetObjects()
        {
            return context.Categories;
        }

        protected override async Task Post_PreInsertCheck(Category category)
        {
            await base.Post_PreInsertCheck(category);

            if(category.parentId != null)
            {
                var parentCategory = await context.Categories.FindAsync(category.parentId);

                if(parentCategory == null)
                    ThrowAction(BadRequest("Nonexistent parent category!"));
            }
        }
    }
}