import { Location } from '@/features/locations/types'
import CheckBoxIcon from '@mui/icons-material/CheckBox'
import CheckBoxOutlineBlankIcon from '@mui/icons-material/CheckBoxOutlineBlank'
import { Autocomplete, Checkbox, TextField } from '@mui/material'

const label = (location: Location) =>
  `${location.locationIdentifier} - ${location.primaryName} @ ${location.secondaryName}`

export interface MultiLocationSelectProps {
  locations: Location[]
  loading?: boolean
  value: Location[]
  onChange: (locations: Location[]) => void
}

export default function MultiLocationSelect({
  locations,
  loading,
  value,
  onChange,
}: MultiLocationSelectProps) {
  return (
    <Autocomplete
      multiple
      disableCloseOnSelect
      limitTags={3}
      loading={loading}
      options={locations}
      value={value}
      onChange={(_, selected) => onChange(selected)}
      getOptionLabel={label}
      isOptionEqualToValue={(a, b) => a.locationIdentifier === b.locationIdentifier}
      renderOption={(props, option, { selected }) => (
        <li {...props} key={option.id}>
          <Checkbox
            icon={<CheckBoxOutlineBlankIcon fontSize="small" />}
            checkedIcon={<CheckBoxIcon fontSize="small" />}
            sx={{ mr: 1 }}
            checked={selected}
          />
          {label(option)}
        </li>
      )}
      renderInput={(params) => (
        <TextField
          {...params}
          label="Locations"
          placeholder={value.length === 0 ? 'Select one or more locations' : ''}
        />
      )}
    />
  )
}
