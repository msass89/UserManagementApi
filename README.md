This is a User Management Api written in C# with Asp.Net allowing the client to get all users or a specific user by id, add new users, update and to delete users.

### 2026-02-12

Improved overall program and folder structure for better readability and maintainability.

### 2026-02-11

Implemented an endpoint for login token generation and middleware for authentificating the generated Token using JWT (JSON Web Token) and Bearer scheme.

Further implemented middleware for global exception handling and event logging.

### 2026-02-09 
Implemented user input validation, atomic operations for unique Id's and a concurrent dictionary in order to prevent concurrency issues in a multi-threaded environment.

### 2026-02-08 

Implemented endpoints following the crud principle.
