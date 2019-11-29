# ChatNeat
A small PoC/example chat service based on Azure SignalR, powered by Azure Functions and Azure Table Storage.

## Assumptions
User profiles and authorization/authentication are someone else's job. 

## Table Design
As Table Storage is NoSQL based, the table design is a little unconventional. There's only one permanent table; the rest are tables that represent groups.
The permanent table contains an eventually-consistent of a) the current total count of groups, and b) one row per group that contains the group's ID, name, current count, and creation time.
Each table that represents a group has a single row of metadata that contains the group's friendly name, and creation time. Every other row is either a user, or a message.

