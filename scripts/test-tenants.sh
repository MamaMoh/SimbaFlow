#!/bin/bash
set -e

API="http://localhost:5117"

# Login
echo "=== LOGIN ==="
LOGIN=$(curl -sf -X POST $API/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@123!"}')
TOKEN=$(echo $LOGIN | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")
echo "Token: ${TOKEN:0:20}..."

# List tenants
echo ""
echo "=== LIST TENANTS ==="
curl -sf -H "Authorization: Bearer $TOKEN" $API/api/tenants | python3 -c "import sys,json;d=json.load(sys.stdin);print(f'Count: {len(d.get(\"data\",[]))}')"

# Create tenant
echo ""
echo "=== CREATE TENANT ==="
RESULT=$(curl -sf -X POST $API/api/tenants -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"agencyName":"Test Agency XYZ","slug":"test-xyz","contactEmail":"xyz@agency.com","adminFirstName":"Test","adminLastName":"Owner","adminEmail":"owner@testagency.com","temporaryPassword":"Welcome@123!"}')
echo $RESULT | python3 -c "import sys,json;d=json.load(sys.stdin);print(f'Success: {d[\"isSuccess\"]}, ID: {d.get(\"data\",\"N/A\")}')"
TENANT_ID=$(echo $RESULT | python3 -c "import sys,json;print(json.load(sys.stdin).get('data',''))")

# Get tenant by ID
echo ""
echo "=== GET TENANT ==="
curl -sf -H "Authorization: Bearer $TOKEN" "$API/api/tenants/$TENANT_ID" | python3 -c "import sys,json;d=json.load(sys.stdin)['data'];print(f'Name: {d[\"name\"]}, Slug: {d[\"slug\"]}')"

# Update tenant
echo ""
echo "=== UPDATE TENANT ==="
curl -sf -X PUT "$API/api/tenants/$TENANT_ID" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Test Agency XYZ Updated","contactEmail":"updated@agency.com","contactPhone":"+251999","maxUsers":100}' | python3 -c "import sys,json;print(f'Update Success: {json.load(sys.stdin)[\"isSuccess\"]}')"

# Login as agency user
echo ""
echo "=== LOGIN AS AGENCY USER ==="
AGENCY_LOGIN=$(curl -s -X POST $API/api/auth/login -H "Content-Type: application/json" -d '{"username":"owner@testagency.com","password":"Welcome@123!"}')
echo $AGENCY_LOGIN | python3 -c "import sys,json;d=json.load(sys.stdin);print(f'Agency Login: {d[\"isSuccess\"]}, User: {d.get(\"data\",{}).get(\"user\",{}).get(\"fullName\",\"N/A\")}')"

# List users - check tenant association
echo ""
echo "=== LIST USERS ==="
curl -sf -H "Authorization: Bearer $TOKEN" "$API/api/users" | python3 -c "
import sys,json
d=json.load(sys.stdin)
for u in d.get('data',{}).get('items',[]):
    print(f'  {u[\"username\"]} - Roles: {u.get(\"roles\",[])}')
"

# Delete tenant
echo ""
echo "=== DELETE TENANT ==="
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API/api/tenants/$TENANT_ID" -H "Authorization: Bearer $TOKEN")
echo "Delete HTTP: $HTTP_CODE"

echo ""
echo "=== ALL TESTS PASSED ==="
