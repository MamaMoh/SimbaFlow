import assert from "node:assert/strict";
import { test } from "node:test";
import {
  hasStoredDefault,
  intakeSelectLabel,
  intakeSelectValue,
  parseIntakeDefaults,
} from "./intake-defaults";

const NONE = "__none__";

/** Shape production GET /api/settings/intake-defaults returns after ToDto. */
const productionGet = {
  isSuccess: true,
  data: {
    gender: "0",
    occupation: "DRIVER",
    religion: "Muslim",
    nationality: "ET",
    passportType: "Diplomatic",
    maritalStatus: "Divorced",
    countryOfTravel: "",
    contractPeriod: "2 Years",
    cvTemplate: "layout2",
    cvTemplates: [{ value: "layout2", name: "Layout 2", description: "x" }],
  },
};

test("parser keeps Male as \"0\" from the live GET envelope", () => {
  const parsed = parseIntakeDefaults(productionGet);
  assert.equal(parsed?.gender, "0");
  assert.equal(parsed?.occupation, "DRIVER");
  assert.equal(parsed?.countryOfTravel, "");
  assert.equal(parsed?.cvTemplate, "layout2");
  assert.deepEqual(parsed?.skills, []);
});

test("parser reads PascalCase Data/Gender the same way", () => {
  const parsed = parseIntakeDefaults({
    Data: { Gender: "0", Occupation: "NANNY" },
  });
  assert.equal(parsed?.gender, "0");
  assert.equal(parsed?.occupation, "NANNY");
});

test("Male \"0\" is a stored default, empty string is not", () => {
  assert.equal(hasStoredDefault("0"), true);
  assert.equal(hasStoredDefault("1"), true);
  assert.equal(hasStoredDefault("DRIVER"), true);
  assert.equal(hasStoredDefault(""), false);
  assert.equal(hasStoredDefault(null), false);
  assert.equal(hasStoredDefault(undefined), false);
});

test("settings labels Male for GET gender \"0\", not the empty sentinel", () => {
  const parsed = parseIntakeDefaults(productionGet);
  assert.equal(intakeSelectValue(parsed?.gender, NONE), "0");
  assert.equal(
    intakeSelectLabel(
      parsed?.gender,
      [
        { value: "1", label: "Female" },
        { value: "0", label: "Male" },
      ],
      "No default — ask each time"
    ),
    "Male"
  );
  assert.equal(intakeSelectLabel(parsed?.countryOfTravel, [], "No default"), "No default");
  assert.equal(intakeSelectLabel("", [{ value: "1", label: "Female" }], "No default — ask each time"), "No default — ask each time");
  assert.equal(intakeSelectLabel("1", [{ value: "1", label: "Female" }], "No default — ask each time"), "Female");
});

test("parser reads skills and default-selected flags", () => {
  const parsed = parseIntakeDefaults({
    isSuccess: true,
    data: {
      gender: "0",
      skills: [
        {
          id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          name: "Cleaning",
          builtInKey: "cleaning",
          isBuiltIn: true,
          isDefaultSelected: true,
          sortOrder: 0,
        },
        { id: "bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee", name: "Driving", isDefaultSelected: false, sortOrder: 1 },
      ],
    },
  });
  assert.equal(parsed?.skills.length, 2);
  assert.equal(parsed?.skills[0]?.builtInKey, "cleaning");
  assert.equal(parsed?.skills[0]?.isDefaultSelected, true);
  assert.equal(parsed?.skills[1]?.name, "Driving");
  assert.equal(parsed?.skills[1]?.isBuiltIn, false);
});

