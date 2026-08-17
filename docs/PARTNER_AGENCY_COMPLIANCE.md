# Partner Agencies & Agency Levels — Compliance Note

**AI-DLC source of truth:** [`aidlc-docs/inception/requirements/partner-agency-and-tenant-licensing.md`](../aidlc-docs/inception/requirements/partner-agency-and-tenant-licensing.md)

This file is a short engineering mirror. Prefer the AI-DLC doc for FRs, stories, and Unit 6 scope.

**Source:** Directive **1126/2018** (MoLS). **Build when:** Unit 6 (Partners + tenant license). **Not** Unit 3.

---

## Architecture (accepted)

```
Platform SuperAdmin → Partner catalog (public)
Tenant Agency Owner → Partner links + agreement (~2y)
Staff intake        → Active links only (by destination country)
```

Enforce: agency **ደረጃ 1–5** ትስስር/country caps (Arts. 18–22); partner **Art. 40** capacity (2/4/8).

AppSheet **OFFICE** = Partner agency. Local branch = `office_id` (Registering Branch).

## Tenant provision gaps

Have today: name, slug, contact, owner account, schema + workflow seed.  
Missing: level, license #/dates, licensed countries, HQ office auto-create, MustChangePassword — see AI-DLC §6.
