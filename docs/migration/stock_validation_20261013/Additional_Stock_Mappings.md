# Additional Stock mappings for the current local UAT project

Checked 13 October 2026 against local project 4 (NexusFlow UAT 2026-10-12). The initial diagnosis made no changes. The mappings were subsequently applied directly at the user request; see Applied update below.

## Findings

The importer converts legacy Code to uppercase, then checks existing SKU strings case-sensitively. Thirty-three existing raw-material SKUs are lowercase in the master, including sdasf. Without a Stock alias, the normalized SDASF becomes a new-SKU candidate. The original preparation guide omitted these 33 required aliases.

The current saved Category mappings contain only MSH. ACCESSORIES is not saved in the local project snapshot. The Accessories master is ID 53, with inventory account 1131, active Asset posting account. WH_MAIN has no override. SDASF already supplies 1131, so no account change is needed.

## Actions in Migration Centre

1. Keep the existing saved mappings. The project already has posted modules; existing mappings cannot be changed or removed. Additional mappings are allowed.
2. In Mapping type select Stock. Enter each uppercase exact legacy code below and choose its existing lowercase SKU under NexusFlow master. Select Add Mapping for each.
3. Keep/add Category ACCESSORIES to Accessories. This is additional to MSH; select Add Mapping if it is absent.
4. Select Save Mappings and Evidence. Refresh/reopen the project and verify that Stock has 33 aliases and Category includes ACCESSORIES before validating.
5. Select Stock only, then Validate Selected Import. No Stock re-upload or database reset is needed for this mapping correction.
6. The composite legacy identity warning is expected and non-blocking. The 938 normalized Stock identities are unique for Code + WarehouseCode + Batch + LayerDate. Do not use row numbers as IDs.

## Exact additional Stock aliases

All rows below use Mapping type Stock. Select the actual SKU master; IDs are reference values for this local database only. Do not put IDs in the Stock CSV Code field.

| Exact legacy code | Select existing SKU | Local master ID | Source row | Inventory account |
|---|---|---:|---:|---|
| HDFG | hdfg | 1725 | 608 | 1131 |
| HGTJHGF | hgtjhgf | 1726 | 609 | 1131 |
| SDASF | sdasf | 1727 | 610 | 1131 |
| RFGRG | rfgrg | 1728 | 611 | 1131 |
| JHTRGF | jhtrgf | 1729 | 612 | 1131 |
| SDF | sdf | 1730 | 613 | 1131 |
| RTYER | rtyer | 1731 | 614 | 1131 |
| AFE | afe | 1752 | 635 | 1131 |
| HYT | hyt | 1753 | 636 | 1131 |
| ,JH,GH | ,jh,gh | 1765 | 648 | 1131 |
| FGFG | fgfg | 1777 | 660 | 1131 |
| STHFG | sthfg | 1787 | 670 | 1131 |
| STHSDFG | sthsdfg | 1795 | 678 | 1131 |
| SRG | srg | 1815 | 698 | 1131 |
| YJT | yjt | 1822 | 705 | 1131 |
| AWE | awe | 1832 | 715 | 1131 |
| JUJT | jujt | 1851 | 734 | 1131 |
| WRE | wre | 1880 | 763 | 1131 |
| LKUI | lkui | 1899 | 782 | 1131 |
| WEFD | wefd | 1907 | 790 | 1131 |
| BRH | brh | 1915 | 798 | 1131 |
| JKYT | jkyt | 1931 | 814 | 1131 |
| WEFWE | wefwe | 1945 | 828 | 1131 |
| ,LIYUK | ,liyuk | 1950 | 833 | 1131 |
| FSDF | fsdf | 1960 | 843 | 1131 |
| BFDB | bfdb | 1979 | 862 | 1131 |
| RYTJRT | rytjrt | 2001 | 884 | 1131 |
| VBCV | vbcv | 2009 | 892 | 1131 |
| OKYUIK | okyuik | 2015 | 898 | 1131 |
| ERT | ert | 2024 | 907 | 1131 |
| GVC | gvc | 2040 | 923 | 1131 |
| WER | wer | 2044 | 927 | 1131 |
| GDS | gds | 2052 | 935 | 1131 |

## Verification limits

A read-only comparison of all 938 uploaded rows against the current masters found no remaining category/brand/unit/product-type/inventory-account mismatch after applying the proposed aliases. This is an offline mapping check, not execution of application validation or import. The local Stock dataset has no posted units. Revalidate through the current UI to check all remaining application rules.

True new SKUs: 81, all using Category MSH and Brand UNBRANDED. Existing SKUs after aliases: 857. You do not need to add all 27 category names simply because they occur in the CSV; existing mapped SKU categories are taken from their masters.

This supplement corrects GUIDE_02_Mappings_and_Defaults.md in the 12 October preparation package. The package files remain unchanged; their quantities, costs, stock controls and trial balance do not need adjustment for these aliases.

## Applied update

All 33 Stock mappings and Category ACCESSORIES -> Accessories were saved directly to local NexusERPLocal, project 4, on 13 October 2026. Existing mapping targets, defaults, column profiles, dataset states and valuation evidence were preserved. The update used a serializable transaction, the application migration lock and active-run guards, a backup of the original project row, and a migration audit record identifying the user-authorized Codex update.

Project Version changed from 13 to 14 and the previous approval was cleared, matching the application setting-change behavior. Exact row counts and SHA256 fingerprints matched for all 25 checked master, financial, inventory, staging, scope, origin, unit and run tables. No imports, approvals, posting, master changes, source-code changes or database reset were performed. All 938 stock rows passed the independent mapping/account consistency check.

Reload Migration Centre and select the saved project before validating Stock. The old browser page can contain unsaved partial mappings; do not save that stale page over the complete saved set. Actual application validation is still required. Chrome no longer had an open window at the final browser check, so no UI validation was run after this database update.

- [Transaction and preservation report](.work/direct-mapping-update-report.json)
- [Saved settings after update](.work/saved-settings-after.json)
- Configuration backup: F:\source\NexusFlow_ERP\docs\migration\stock_validation_20261013\.work\project4-before-mappings-20261013-182208.json
- Backup SHA256: CA58835ACCEB5B9690648F88E17C8ADFF94F2CBB7E9B1E30F13AB851DD93B298
