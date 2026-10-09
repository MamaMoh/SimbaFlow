"use client";

import PhoneInput from "react-phone-input-2";
import "react-phone-input-2/lib/style.css";

interface PhoneInputFieldProps {
  value?: string;
  onChange: (value: string) => void;
  placeholder?: string;
  /**
   * Which country the box starts on, as an ISO-2 code.
   *
   * Ethiopia for everyone standing in the office — the candidate, her relative, the second
   * contact. The sponsor is in the destination country, and typing a Saudi number into a box
   * that had pre-selected +251 produced numbers the agency could not call back.
   */
  country?: string;
}

export function PhoneInputField({
  value,
  onChange,
  placeholder = "Enter phone number",
  country = "et",
}: PhoneInputFieldProps) {
  return (
    <div className="phone-input-wrapper">
      <PhoneInput
        country={country}
        value={value}
        onChange={(phone) => onChange(`+${phone}`)}
        placeholder={placeholder}
        enableSearch
        searchPlaceholder="Search country..."
        inputClass="!w-full !h-9 !text-sm !rounded-md !border !border-input !bg-transparent !pl-12"
        buttonClass="!rounded-l-md !border !border-input !bg-transparent !border-r-0"
        containerClass="!w-full"
        dropdownClass="!rounded-md !shadow-md !border !border-input"
        searchClass="!rounded-sm !border !border-input !text-sm"
      />
    </div>
  );
}
