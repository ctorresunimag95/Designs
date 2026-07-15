-- Seed data for Todos table

IF NOT EXISTS (SELECT 1 FROM Todos)
BEGIN
    INSERT INTO Todos (Title, Description, IsCompleted, DueDate, CreatedAt)
    VALUES
        (
            'Setup CI/CD pipeline',
            'Configure GitHub Actions workflow for build and deploy.',
            0,
            DATEADD(DAY, 7, CAST(GETDATE() AS DATE)),
            GETDATE()
        ),
        (
            'Write unit tests',
            'Add unit tests for the data and API layers.',
            0,
            DATEADD(DAY, 14, CAST(GETDATE() AS DATE)),
            GETDATE()
        );
END
