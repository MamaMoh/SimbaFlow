#!/bin/bash
API="http://localhost:5117"

echo "Logging in as admin..."
TOKEN=$(curl -sf -X POST $API/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@123!"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

echo "Creating Agency 1: Ethio Star Labour Export..."
curl -sf -X POST $API/api/tenants -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"agencyName":"Ethio Star Labour Export","slug":"ethio-star","contactEmail":"info@ethiostar.et","contactPhone":"+251911234567","adminFirstName":"Tadesse","adminLastName":"Bekele","adminEmail":"tadesse@ethiostar.et","temporaryPassword":"Agency@123!"}'
echo ""

echo "Creating Agency 2: Addis Manpower PLC..."
curl -sf -X POST $API/api/tenants -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"agencyName":"Addis Manpower PLC","slug":"addis-manpower","contactEmail":"contact@addismanpower.com","contactPhone":"+251922345678","adminFirstName":"Hana","adminLastName":"Girma","adminEmail":"hana@addismanpower.com","temporaryPassword":"Agency@123!"}'
echo ""

echo "Creating Agency 3: Golden Gate Employment..."
curl -sf -X POST $API/api/tenants -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"agencyName":"Golden Gate Employment","slug":"golden-gate","contactEmail":"info@goldengate.et","contactPhone":"+251933456789","adminFirstName":"Mohammed","adminLastName":"Ali","adminEmail":"mohammed@goldengate.et","temporaryPassword":"Agency@123!"}'
echo ""

echo "Creating sample candidates..."
curl -sf -X POST $API/api/candidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"firstName":"Abdi","lastName":"Mohammed","passportNumber":"EP1001001","dateOfBirth":"1995-03-15","gender":0,"nationality":"Ethiopia","phoneNumber":"+251911111111","countryOfTravel":"Saudi Arabia","officeName":"Al Rajhi","officeId":"00000000-0000-0000-0000-000000000000"}'
echo ""
curl -sf -X POST $API/api/candidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"firstName":"Fatima","lastName":"Ali","passportNumber":"EP1001002","dateOfBirth":"1998-07-20","gender":1,"nationality":"Ethiopia","phoneNumber":"+251922222222","countryOfTravel":"UAE","officeName":"Dubai Office","officeId":"00000000-0000-0000-0000-000000000000"}'
echo ""
curl -sf -X POST $API/api/candidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"firstName":"Yonas","lastName":"Tadesse","passportNumber":"EP1001003","dateOfBirth":"1993-11-05","gender":0,"nationality":"Ethiopia","phoneNumber":"+251933333333","countryOfTravel":"Kuwait","officeName":"Kuwait Agency","officeId":"00000000-0000-0000-0000-000000000000"}'
echo ""
curl -sf -X POST $API/api/candidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"firstName":"Meron","lastName":"Hailu","passportNumber":"EP1001004","dateOfBirth":"1997-01-30","gender":1,"nationality":"Ethiopia","phoneNumber":"+251944444444","countryOfTravel":"Qatar","officeName":"Doha Office","officeId":"00000000-0000-0000-0000-000000000000"}'
echo ""
curl -sf -X POST $API/api/candidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"firstName":"Dawit","lastName":"Fikru","passportNumber":"EP1001005","dateOfBirth":"1996-05-12","gender":0,"nationality":"Ethiopia","phoneNumber":"+251955555555","countryOfTravel":"Saudi Arabia","officeName":"Jeddah Branch","officeId":"00000000-0000-0000-0000-000000000000"}'
echo ""

echo "Creating sample roles..."
curl -sf -X POST $API/api/roles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Embassy Officer","code":"embassy-officer","description":"Handles embassy clearances and visa processing","sortOrder":1,"permissions":["candidate.read","embassy.read","embassy.update","workflow.view","workflow.execute"]}'
echo ""
curl -sf -X POST $API/api/roles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Finance Manager","code":"finance-manager","description":"Manages commissions and accounting","sortOrder":2,"permissions":["candidate.read","commission.read","commission.create","commission.update","accounting.read","accounting.post","report.view","report.export"]}'
echo ""
curl -sf -X POST $API/api/roles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Data Entry Clerk","code":"data-entry","description":"Registers candidates and manages documents","sortOrder":3,"permissions":["candidate.read","candidate.create","candidate.update","workflow.view"]}'
echo ""

echo ""
echo "=== SEED COMPLETE ==="
echo "Agencies: 3 (ethio-star, addis-manpower, golden-gate)"
echo "Candidates: 5"
echo "Custom Roles: 3 (embassy-officer, finance-manager, data-entry)"
echo ""
echo "Login credentials:"
echo "  Platform Admin: admin / Admin@123!"
echo "  Ethio Star:     tadesse@ethiostar.et / Agency@123!"
echo "  Addis Manpower: hana@addismanpower.com / Agency@123!"
echo "  Golden Gate:    mohammed@goldengate.et / Agency@123!"
