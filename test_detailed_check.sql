-- Check the database for any potential issues with complaints and students
-- 1. Verify all complaints have matching students
SELECT
    c.Id AS ComplaintId,
    c.Title,
    c.StudentId,
    CASE WHEN u.Id IS NULL THEN 'MISSING USER!' ELSE 'OK' END AS UserStatus,
    u.FullName,
    u.Email
FROM Complaints c
LEFT JOIN AspNetUsers u ON c.StudentId = u.Id;

-- 2. Check for any NULL values in critical fields
SELECT
    c.Id,
    c.Title,
    CASE WHEN c.Title IS NULL THEN 'NULL TITLE' ELSE 'OK' END AS TitleStatus,
    CASE WHEN c.Description IS NULL THEN 'NULL DESC' ELSE 'OK' END AS DescStatus,
    CASE WHEN c.StudentId IS NULL THEN 'NULL STUDENT' ELSE 'OK' END AS StudentStatus
FROM Complaints c;

-- 3. Verify the Student navigation is loadable
SELECT
    c.Id AS ComplaintId,
    c.Title,
    c.Category,
    c.Status,
    u.FullName AS StudentName,
    c.CreatedAt,
    c.UpdatedAt
FROM Complaints c
INNER JOIN AspNetUsers u ON c.StudentId = u.Id
ORDER BY c.Id;
