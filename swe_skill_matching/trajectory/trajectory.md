1. Audit the Original Code (Identify Scaling Issues)
   I audited the original implementation and identified multiple scalability problems. The code loaded full tables into memory for filtering and sorting, applied pagination after materialization, repeatedly recalculated deal values, and relied on N+1 query patterns. This approach would degrade rapidly as data volume grows.
   Reference on N+1 problem: [https://youtu.be/lptxhwzJK1g](https://youtu.be/lptxhwzJK1g)
   Practical explanation: [https://michaelkasingye.medium.com/optimizing-database-queries-avoiding-the-n-1-query-problem-438476198983](https://michaelkasingye.medium.com/optimizing-database-queries-avoiding-the-n-1-query-problem-438476198983)

2. Define a Performance Contract First
   Before refactoring, I defined strict performance rules. Filtering and ordering must remain in the database, ordering must be stable, keyset pagination must be used instead of offset pagination, N+1 query patterns must be forbidden, and per-request aggregations must be avoided.
   General guidance on performance-driven design: [https://youtu.be/o25GCdzw8hs](https://youtu.be/o25GCdzw8hs)

3. Rework the Data Model for Efficiency
   I introduced a ContactMetrics table to store precomputed values such as cached deal value and interaction count. This removed the need to join heavy deals and interactions tables during search operations, significantly reducing query cost.
   SQL optimization strategies: [https://horkan.com/2024/08/19/practical-strategies-for-optimising-sql-refactoring-indexing-and-orm-best-practices](https://horkan.com/2024/08/19/practical-strategies-for-optimising-sql-refactoring-indexing-and-orm-best-practices)

4. Rebuild the Search as a Projection-First Pipeline
   The search pipeline was rebuilt to project only the required fields into lightweight data shapes. This prevents the ORM from materializing large entity graphs and reduces memory pressure and query execution time.

5. Move All Filters to the Database
   All filtering logic was pushed into SQL. Filters such as city, date, deal stage, and minimum deal value are now translated into server-side predicates that benefit directly from existing indexes.

6. Replace Heavy Joins with EXISTS for Tag Filtering
   Tag filtering was rewritten using EXISTS subqueries instead of eager-loading collections. This avoids cartesian explosions and keeps result sets compact and predictable.
   Reference on EXISTS vs joins: [https://use-the-index-luke.com/sql/joins/existence](https://use-the-index-luke.com/sql/joins/existence)

7. Implement Stable Ordering and Keyset Pagination
   I implemented stable ordering using last contact date, last name, first name, and ID. Keyset pagination with cursor-based seek predicates enables efficient deep paging without expensive OFFSET scans.
   Keyset pagination explanation: [https://youtu.be/rhOVF82KY7E](https://youtu.be/rhOVF82KY7E)

8. Eliminate N+1 Queries for Enrichment
   To enrich results, the system now fetches a page of contacts first, then loads all related tags in a single follow-up query. This removes per-row fetches and guarantees a fixed query count per request.
   More on fixing N+1 patterns: [https://www.pingcap.com/article/how-to-efficiently-solve-the-n1-query-problem](https://www.pingcap.com/article/how-to-efficiently-solve-the-n1-query-problem)

9. Normalize Data for Case-Insensitive Searches
   I added a NormalizedName column to the Tag table and normalized incoming request values. This enables case-insensitive filtering without function calls that would otherwise disable index usage.

10. Result: Predictable and Measurable Performance
    The final solution consistently executes two queries per request, avoids heavy tables during search, maintains deterministic ordering, and delivers measurable performance improvements through index-friendly queries and efficient pagination.
