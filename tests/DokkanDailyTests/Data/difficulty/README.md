# Difficulty OCR fixtures

The eight images in this directory are original uploads downloaded read-only from
Azure on 2026-09-09. Their difficulty labels were checked visually before adding
expectations. The two `special-battle-*-super` files show SUPER, the two
`special-battle-*-super3` files show SUPER3, and the four `heroine-*` files show
SUPER2. These cover Global and Japanese layouts, different resolutions, and both
single-star and multiple-star displays. No stars are used to infer difficulty.

`expectations.json` also covers the existing screenshot corpus. Values describe
the difficulty returned by the complete OCR pipeline. Null means the complete
screenshot is rejected. In particular, `ipad.jpg` already has null expectations
for nickname, time, and item usage in its original snapshot; it remains rejected.

Run `dotnet test tests/DokkanDailyTests/DokkanDailyTests.csproj --filter FullyQualifiedName~DifficultyTests`.
