declare module "react-select-country-list" {
  export default function countryList(): {
    getData: () => { value: string; label: string }[];
  };
}

