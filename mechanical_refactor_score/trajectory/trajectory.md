1. Audit the Original Code (Identify Scaling Problems):
   I audited the original code. It loaded full tables into memory for filtering and sorting, applied pagination too late, repeatedly recalculated deal values, and used N+1 queries — all of which would not scale.
   Learn about the N+1 query problem and why it’s bad: [https://youtu.be/lptxhwzJK1g](https://youtu.be/lptxhwzJK1g)
   Practical article explaining N+1 and how to fix it: Optimizing Database Queries: Avoiding the N+1 Query Problem
   Link: [https://michaelkasingye.medium.com/optimizing-database-queries-avoiding-the-n-1-query-problem-438476198983](https://michaelkasingye.medium.com/optimizing-database-queries-avoiding-the-n-1-query-problem-438476198983)

2. Define a Performance Contract First
   I defined performance conditions: filtering and ordering must remain in the database, ordering must be stable, use keyset pagination, forbid N+1 patterns, and avoid per-request aggregations.
   [https://youtu.be/o25GCdzw8hs](https://youtu.be/o25GCdzw8hs)

3. Rework the Data Model for Efficiency
   I introduced a new ContactMetrics table storing precomputed cached values like deal value and interaction count. This prevents expensive joins on heavy tables.
   You can see more about SQL optimization basics here: Practical strategies for SQL refactoring, joins, indexes, and ORM best practices
   Link: [https://horkan.com/2024/08/19/practical-strategies-for-optimising-sql-refactoring-indexing-and-orm-best-practices?utm](https://horkan.com/2024/08/19/practical-strategies-for-optimising-sql-refactoring-indexing-and-orm-best-practices?utm)

4. Rebuild the Search as a Projection-First Pipeline
   The pipeline now selects only essential fields into lightweight shapes, reducing expensive ORM entity materialization.

5. Move Filters to the Database (Server-Side)
   All filters (city, date, deal stage, minimum value) now translate into SQL predicates that benefit from existing indexes.

6. Use EXISTS Instead of Cartesian Joins / Heavy Tag Filtering
   Tag filtering now uses an EXISTS subquery instead of eager collection loads to prevent exploding result sets.

7. Stable Ordering + Keyset Pagination
   I implemented stable ordering and keyset pagination.
   Learn why OFFSET hurts performance and how keyset pagination drastically improves efficiency: [https://youtu.be/rhOVF82KY7E](https://youtu.be/rhOVF82KY7E)

8. Eliminate N+1 Queries for Enrichment
   I eliminated N+1 patterns by batching related fetches. For a page of contacts, tags are loaded in one follow-up query rather than one per contact.
   Detailed strategies for spotting and fixing N+1 (ORM or SQL based) are covered here: How to efficiently solve the N+1 query problem
   Link: [https://www.pingcap.com/article/how-to-efficiently-solve-the-n1-query-problem/?utm](https://www.pingcap.com/article/how-to-efficiently-solve-the-n1-query-problem/?utm)

9. Normalize for Case-Insensitive Searches
   Added a normalized version (e.g., NormalizedName) for tags to ensure consistent case-insensitive filtering without function calls that kill indexes.

10. Result: Measurable Performance Gains + Predictable Signals
    The solution consistently uses two queries per request, never touches heavy tables during search, stays index-friendly, and exhibits measurable performance improvements (fewer query scans, fast keyset pagination, and no N+1 patterns).
