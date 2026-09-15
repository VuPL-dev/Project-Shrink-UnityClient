# Global Data Architecture - Project Shrink

Based on the system requirements and context diagram, the database architecture leverages a polyglot persistence approach using PostgreSQL, MongoDB, and Redis.

## 1. PostgreSQL (Relational Data)
**Purpose:** Stores highly structured, relational data requiring strict consistency and ACID compliance.
- **Accounts & Authentication:**
  - `Users` (Player, Moderator, Shop Manager, Admin credentials and profiles)
  - `Roles` & `Permissions`
- **Transactions & Economics:**
  - `Payments` & `Transaction Requests` (Financial records)
  - `Sales Reports` (Shop Manager metrics)
- **Configuration & Core Systems:**
  - `Game Configuration` (Admin settings)
  - `System Analytics & Metrics`
  - `Quest & Puzzle Configurations` (Core logic defined by Moderators)

## 2. MongoDB (Document & Unstructured Data)
**Purpose:** Stores flexible, document-based data that may change schemas frequently or contains heavily nested structures.
- **Game State & Player Data:**
  - `Player Inventories` (Bag Items, collected resources)
  - `Crafting & Resource Recipes/Configs`
  - `Player Save States` (JSON save data, coordinates, quest progress)
- **Game Content:**
  - `Product/Item Information` (Shop Manager data)
  - `NPC Data` (Shop NPC dialogue and inventory)
  - `Dialogue Trees & Narrative Evidence logs`

## 3. Redis (In-Memory Data Store)
**Purpose:** Fast read/write access for caching, temporary state, and real-time operations.
- **Session & State Management:**
  - `Playing Status` (Online/Offline status, current active session)
  - Session tokens / JWT blocklists
- **Caching:**
  - Frequently accessed game configurations (e.g., loaded by Shop Manager or Admin)
  - Caching leaderboard or fast-access analytics metrics.
- **Rate Limiting:** API rate limiting to protect the backend services.
