# Issue 34 checkpoints

Base: PR #33 `807c6ab1d9aa59ff2980dc956b9eb4f7950a74d3`; preserves #30 and #32.

| Series | Executed checks | Working scope | Open barrier |
|---|---|---|---|
| A | 26 niche/colony NUnit cases passed; continuous chunk gradients, conservative mixing/decay, one-time migration, v1-v6 readers and profile/stock round-trip | Smooth forcing, moving finite resource parcels, oxygen/nutrient ledgers, v6 storage | Niche physiology and offspring matter payment are integrated in B; no Unity run |
| B | 46 niche/colony/lifecycle cases passed; paid matter growth/births, no DNA rewrites, path/oxygen/thermal trade-offs, short energy/matter/gas ledger checks | Niche physiology, separate organic paths, reduced chemical carrier, paid offspring matter, detrital matter and predation transfer, biomass light attenuation | 600s population calibration/replication is running; float ledgers use declared 0.02% matter/gas tolerance; no Unity run |
| C | 30 niche/survey/localization cases passed; source identity/position/finite stock, discovery energy credit, read-only map/history, history restore, actual light attenuation | Ecological map layers, local deficit explanation, bounded generation history, physical vent outcrops and persistent finite source parcel; NPC sensing/feeding throttle uses real movement cost | Final 8 seeded 600s runs complete with different inherited outcomes; repeat/full Core/actual chunk-world checks are in D; Unity appearance/layout remains unverified |
| D | 296-case full local pass before final motor refinement; 23 niche and 8 selection cases passed after it; 8 x 600s + exact repeated CSV; 4 ordinary generated worlds x 600s | Explicit legacy ledger epoch and old-rule catch-up, shared NPC motor duty, overlap budget, complete data/report, CPU measurement and dormant capture route | Final full GitHub Core CI reported in PR; Unity/API/PlayMode/real images remain NOT RUN |
