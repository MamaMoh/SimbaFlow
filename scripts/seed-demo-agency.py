#!/usr/bin/env python3
"""Seed demo-agency test data for local QA. Does not print tokens."""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request
from datetime import date, timedelta
from typing import Any

API = "http://localhost:5117"
USER = "owner@demo.agency"
PASSWORD = "Agency@123!"


def req(method: str, path: str, token: str | None = None, body: dict | None = None) -> tuple[int, Any]:
    data = None if body is None else json.dumps(body).encode()
    headers = {"Content-Type": "application/json", "Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(f"{API}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=60) as resp:
            raw = resp.read().decode() or ""
            if not raw.strip():
                return resp.status, {}
            try:
                return resp.status, json.loads(raw)
            except json.JSONDecodeError:
                return resp.status, {"raw": raw}
    except urllib.error.HTTPError as e:
        raw = e.read().decode() or ""
        try:
            payload = json.loads(raw) if raw.strip() else {"error": str(e)}
        except json.JSONDecodeError:
            payload = {"error": raw or str(e)}
        return e.code, payload
    except urllib.error.URLError as e:
        return 0, {"error": str(e)}


def unwrap(payload: Any) -> Any:
    if isinstance(payload, dict) and "data" in payload:
        return payload["data"]
    return payload


def login() -> str:
    code, payload = req("POST", "/api/auth/login", body={"username": USER, "password": PASSWORD})
    if code != 200:
        raise SystemExit(f"Login failed HTTP {code}: {payload.get('error') or payload}")
    data = unwrap(payload) or {}
    token = data.get("accessToken") or data.get("AccessToken")
    if not token:
        raise SystemExit(f"Login OK but no accessToken in response keys={list(data.keys())}")
    print(f"Logged in as {USER}")
    return token


def pick_office(token: str) -> tuple[str, str]:
    # Candidate UI uses departments as "offices" (office.read)
    code, payload = req("GET", "/api/departments", token)
    if code != 200:
        raise SystemExit(f"Departments failed HTTP {code}: {payload.get('error') or payload}")
    items = unwrap(payload) or []
    if not isinstance(items, list) or not items:
        raise SystemExit("No departments/offices found — DepartmentSeeder may not have run")
    preferred = next(
        (x for x in items if "office" in str(x.get("name") or x.get("Name") or "").lower()
         or "branch" in str(x.get("name") or x.get("Name") or "").lower()),
        items[0],
    )
    oid = preferred.get("id") or preferred.get("Id")
    name = preferred.get("name") or preferred.get("Name") or "Office"
    print(f"Using office: {name} ({oid})")
    return str(oid), str(name)


def create_candidate(token: str, office_id: str, office_name: str, spec: dict) -> str | None:
    body = {
        "firstName": spec["firstName"],
        "lastName": spec["lastName"],
        "middleName": None,
        "passportNumber": spec["passport"],
        "dateOfBirth": spec["dob"],
        "gender": spec["gender"],
        "nationality": "Ethiopia",
        "phoneNumber": spec.get("phone"),
        "email": None,
        "address": "Addis Ababa",
        "city": "Addis Ababa",
        "country": "Ethiopia",
        "labourId": spec.get("labourId"),
        "countryOfTravel": spec["country"],
        "officeName": office_name,
        "contractDate": date.today().isoformat(),
        "officeId": office_id,
    }
    code, payload = req("POST", "/api/candidates", token, body)
    if code in (200, 201) and (payload.get("isSuccess") is True or unwrap(payload)):
        cid = unwrap(payload)
        print(f"  + candidate {spec['firstName']} {spec['lastName']} → {cid}")
        return str(cid)
    if code == 409:
        print(f"  ~ skip {spec['passport']} (already exists)")
        return None
    print(f"  ! create failed {spec['passport']} HTTP {code}: {payload.get('error') or payload}")
    return None


def actions(token: str, cid: str) -> list[dict]:
    code, payload = req("GET", f"/api/workflow/{cid}/actions", token)
    if code != 200:
        print(f"  ! actions HTTP {code}: {payload.get('error') or payload}")
        return []
    data = unwrap(payload) or []
    return data if isinstance(data, list) else []


def find_action(acts: list[dict], label_substr: str) -> dict | None:
    needle = label_substr.lower()
    for a in acts:
        label = str(a.get("buttonLabel") or a.get("ButtonLabel") or "")
        enabled = a.get("isEnabled", a.get("IsEnabled", True))
        if needle in label.lower() and enabled:
            return a
    return None


def transition(token: str, cid: str, label: str) -> bool:
    act = find_action(actions(token, cid), label)
    if not act:
        print(f"  ! no enabled action matching '{label}'")
        return False
    rule_id = act.get("transitionRuleId") or act.get("TransitionRuleId")
    code, payload = req(
        "POST",
        f"/api/workflow/{cid}/transition",
        token,
        {"transitionRuleId": rule_id, "notes": "seed"},
    )
    ok = code == 200 and payload.get("isSuccess", True)
    err = payload.get("error") or payload
    print(f"  → {label}: {'OK' if ok else f'FAIL {code} {err}'}")
    return bool(ok)


def set_status(token: str, cid: str, track: str, value: str) -> bool:
    code, payload = req(
        "POST",
        f"/api/workflow/{cid}/status",
        token,
        {"trackName": track, "newValue": value, "notes": "seed"},
    )
    ok = code == 200 and payload.get("isSuccess", True)
    err = payload.get("error") or payload
    print(f"  → status {track}={value}: {'OK' if ok else f'FAIL {code} {err}'}")
    return bool(ok)


def embassy_post(token: str, path: str, body: dict) -> bool:
    code, payload = req("POST", path, token, body)
    ok = code == 200 and payload.get("isSuccess", True)
    err = payload.get("error") or payload
    print(f"  → {path}: {'OK' if ok else f'FAIL {code} {err}'}")
    return bool(ok)


def advance_to_embassy(token: str, cid: str) -> bool:
    if not transition(token, cid, "New Contracts"):
        return False
    if not set_status(token, cid, "status", "Ready"):
        # Some engines use empty track / stage status name only
        set_status(token, cid, "new_contracts", "Ready")
    return transition(token, cid, "Embassy")


def main() -> int:
    print("=== Seed demo agency test data ===")
    code, _ = req("GET", "/health")
    if code != 200:
        print("API not healthy on :5117")
        return 1

    token = login()
    office_id, office_name = pick_office(token)

    people = [
        {"firstName": "Abdi", "lastName": "Mohammed", "passport": "EP9001001", "dob": "1995-03-15", "gender": 0, "phone": "+251911100001", "country": "Saudi Arabia", "labourId": "LAB-9001", "path": "embassy_pending"},
        {"firstName": "Fatima", "lastName": "Ali", "passport": "EP9001002", "dob": "1998-07-20", "gender": 1, "phone": "+251911100002", "country": "UAE", "labourId": "LAB-9002", "path": "embassy_pending"},
        {"firstName": "Yonas", "lastName": "Tadesse", "passport": "EP9001003", "dob": "1993-11-05", "gender": 0, "phone": "+251911100003", "country": "Kuwait", "labourId": "LAB-9003", "path": "embassy_medical"},
        {"firstName": "Meron", "lastName": "Hailu", "passport": "EP9001004", "dob": "1997-01-30", "gender": 1, "phone": "+251911100004", "country": "Qatar", "labourId": "LAB-9004", "path": "embassy_ready_lmis"},
        {"firstName": "Dawit", "lastName": "Fikru", "passport": "EP9001005", "dob": "1996-05-12", "gender": 0, "phone": "+251911100005", "country": "Saudi Arabia", "labourId": "LAB-9005", "path": "intake"},
        {"firstName": "Sara", "lastName": "Bekele", "passport": "EP9001006", "dob": "1999-09-09", "gender": 1, "phone": "+251911100006", "country": "UAE", "labourId": "LAB-9006", "path": "new_contracts"},
        {"firstName": "Kebede", "lastName": "Girma", "passport": "EP9001007", "dob": "1994-02-18", "gender": 0, "phone": "+251911100007", "country": "Bahrain", "labourId": "LAB-9007", "path": "embassy_pending"},
        {"firstName": "Hanna", "lastName": "Wolde", "passport": "EP9001008", "dob": "1996-12-01", "gender": 1, "phone": "+251911100008", "country": "Oman", "labourId": "LAB-9008", "path": "embassy_visa"},
    ]

    created: list[tuple[str, str]] = []
    print("Creating candidates...")
    for p in people:
        cid = create_candidate(token, office_id, office_name, p)
        if cid:
            created.append((cid, p["path"]))

    # If creates were skipped (409), try to find via list and advance existing EP900* by listing
    if len(created) < 3:
        code, payload = req("GET", "/api/candidates?page=1&pageSize=50", token)
        items = (unwrap(payload) or {}).get("items") or (unwrap(payload) or {}).get("Items") or []
        for it in items:
            passport = it.get("passportNumber") or it.get("PassportNumber") or ""
            if passport.startswith("EP900"):
                cid = str(it.get("id") or it.get("Id"))
                path = next((p["path"] for p in people if p["passport"] == passport), "embassy_pending")
                if cid and all(c != cid for c, _ in created):
                    created.append((cid, path))

    appt = (date.today() + timedelta(days=7)).isoformat()

    print("Advancing workflow...")
    for cid, path in created:
        print(f"Candidate {cid} path={path}")
        if path == "intake":
            continue
        if path == "new_contracts":
            transition(token, cid, "New Contracts")
            continue

        if not advance_to_embassy(token, cid):
            continue

        if path == "embassy_pending":
            continue

        if path in ("embassy_medical", "embassy_ready_lmis", "embassy_visa"):
            embassy_post(
                token,
                f"/api/embassy/candidates/{cid}/medical/book",
                {"appointmentDate": appt, "facilityName": "Seed Clinic", "notes": "seed"},
            )
            embassy_post(
                token,
                f"/api/embassy/candidates/{cid}/medical/result",
                {"result": "Fit", "notes": "seed"},
            )
            embassy_post(
                token,
                f"/api/embassy/candidates/{cid}/tasheer/book",
                {"appointmentDate": appt, "notes": "seed"},
            )
            embassy_post(
                token,
                f"/api/embassy/candidates/{cid}/tasheer/result",
                {"result": "Book Done", "notes": "seed"},
            )

        if path in ("embassy_ready_lmis", "embassy_visa"):
            # medical+tasheer done already enables LMIS mirror; set visa ready for case executive
            embassy_post(token, f"/api/embassy/candidates/{cid}/visa/ready", {"notes": "seed"})

        if path == "embassy_visa":
            embassy_post(
                token,
                f"/api/embassy/candidates/{cid}/visa/submit",
                {"submittedAt": date.today().isoformat(), "notes": "seed"},
            )

    # Board sanity (auth)
    for path, label in [
        ("/api/embassy/board?page=1&pageSize=20", "Embassy board"),
        ("/api/candidates?page=1&pageSize=20", "Candidates"),
    ]:
        code, payload = req("GET", path, token)
        data = unwrap(payload) or {}
        total = data.get("totalCount") or data.get("TotalCount") or len(data.get("items") or data.get("Items") or [])
        print(f"{label}: HTTP {code}, count≈{total}")

    print("=== DONE ===")
    print("Login: owner@demo.agency / Agency@123!")
    print("Check: /workflow/embassy, /candidates, /workflow/case-executive, /workflow/lmis")
    return 0


if __name__ == "__main__":
    sys.exit(main())
