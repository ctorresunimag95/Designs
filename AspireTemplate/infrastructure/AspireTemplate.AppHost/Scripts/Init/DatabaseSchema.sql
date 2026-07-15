IF DB_ID('AppDB') IS NULL
    CREATE DATABASE [AppDB];
GO

USE [AppDB];
GO

CREATE TABLE Todos (
    id INT PRIMARY KEY IDENTITY(1,1),
    title VARCHAR(255) NOT NULL,
    description TEXT,
    is_completed BIT DEFAULT 0,
    due_date DATE,
    created_at DATETIME DEFAULT GETDATE()
);

GO