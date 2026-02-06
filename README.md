# unknown-civilization-dictionary
# Alien Dictionary (Unknown Alphabet Order)

Given a list of words sorted lexicographically according to an unknown language, infer the order of all characters used in that language.

## Approach
- Build a directed graph of precedence constraints between characters.
- Run a Topological Sort (Kahn or DFS) to produce a valid alphabet order.
- Validate edge cases:
  - Prefix conflict (e.g., ["abc", "ab"] is invalid)
  - Cycles in constraints (no valid order)

## Tech
- Language: (to be defined)
- Tests: (to be defined)

## Status
Work in progress.

