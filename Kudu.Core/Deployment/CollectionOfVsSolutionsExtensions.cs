using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public static class CollectionOfVsSolutionsExtensions
    {
        public static void ThrowIfMultipleSolutionsFound(this IEnumerable<VsSolution> solutions)
        {
            if (solutions.Count() > 1)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  Resources.Error_AmbiguousSolutions,
                                                                  String.Join(", ", solutions.Select(s => s.Path))));
            }
        }
    }
}