import { useState } from "react";

/** Existing values are selected; new names require an explicit choice. */
export default function RuntimeKey({
  value,
  keys,
  onChange,
  label,
  pl,
}: {
  value: string;
  keys: string[];
  onChange: (value: string) => void;
  label: string;
  pl: boolean;
}) {
  const [creating, setCreating] = useState(false);
  const options = [...new Set([...keys, ...(value ? [value] : [])])];
  return (
    <div className="runtime-key">
      <label>
        {label}
        <select
          aria-label={label}
          required
          value={creating ? "__create__" : value}
          onChange={(e) => {
            const create = e.target.value === "__create__";
            setCreating(create);
            onChange(create ? "" : e.target.value);
          }}
        >
          <option value="">{pl ? "Wybierz wartość" : "Choose a value"}</option>
          {options.map((key) => (
            <option key={key} value={key}>
              {key}
            </option>
          ))}
          <option value="__create__">
            {pl ? "+ Utwórz nową nazwę" : "+ Create a new name"}
          </option>
        </select>
      </label>
      {creating && (
        <label>
          {pl ? "Nowa nazwa" : "New name"}
          <input
            required
            maxLength={200}
            value={value}
            onChange={(e) => onChange(e.target.value)}
          />
        </label>
      )}
    </div>
  );
}
