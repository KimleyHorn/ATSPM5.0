import { EventCodeInfo, getEventCodes } from '@/features/charts/api/getRawEvents'
import CheckBoxIcon from '@mui/icons-material/CheckBox'
import CheckBoxOutlineBlankIcon from '@mui/icons-material/CheckBoxOutlineBlank'
import { Autocomplete, Checkbox, TextField } from '@mui/material'
import { useMemo } from 'react'
import { useQuery } from 'react-query'

const label = (code: EventCodeInfo) => `${code.code} - ${code.name}`

// Group headings in the picker; codes the enumeration doesn't categorize go last
const OTHER = 'Other'

export interface EventCodeSelectProps {
  value: EventCodeInfo[]
  onChange: (codes: EventCodeInfo[]) => void
}

export default function EventCodeSelect({
  value,
  onChange,
}: EventCodeSelectProps) {
  const { data, isLoading } = useQuery('rawEventCodes', getEventCodes, {
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })

  // The picker needs options sorted by group so each heading appears once
  const options = useMemo(
    () =>
      [...(data ?? [])].sort((a, b) => {
        const ga = a.category || OTHER
        const gb = b.category || OTHER
        if (ga === gb) return a.code - b.code
        if (ga === OTHER) return 1
        if (gb === OTHER) return -1
        return ga.localeCompare(gb)
      }),
    [data]
  )

  return (
    <Autocomplete
      multiple
      disableCloseOnSelect
      limitTags={3}
      loading={isLoading}
      options={options}
      value={value}
      onChange={(_, selected) => onChange(selected)}
      groupBy={(option) => option.category || OTHER}
      getOptionLabel={label}
      isOptionEqualToValue={(a, b) => a.code === b.code}
      renderOption={(props, option, { selected }) => (
        <li {...props} key={option.code}>
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
          label="Event codes"
          placeholder={value.length === 0 ? 'All event codes' : ''}
          helperText="Leave empty to include every event code"
        />
      )}
    />
  )
}
