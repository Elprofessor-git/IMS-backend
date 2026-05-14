using System.Collections.Generic;

namespace Backend_Gestion_Magasin_API.Dtos
{
    public class PaginatedResponse<T>
    {
        public int PageNumber { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages { get; }
        public IEnumerable<T> Data { get; }

        public PaginatedResponse(IEnumerable<T> data, int pageNumber, int pageSize, int totalCount)
        {
            Data = data;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}
