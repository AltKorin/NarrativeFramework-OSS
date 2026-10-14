"""Domain failure shared by input validation, HTTP and MCP services."""


class DomainError(Exception):
    def __init__(self, message, status=400):
        self.status = status
        super().__init__(message)
