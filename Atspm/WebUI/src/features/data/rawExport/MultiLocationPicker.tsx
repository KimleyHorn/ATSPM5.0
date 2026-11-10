import { useLatestVersionOfAllLocations } from '@/features/locations/api'
import { Filters } from '@/features/locations/components/selectLocation/SelectLocation'
import SelectLocationMap from '@/features/locations/components/selectLocationMap'
import { Location } from '@/features/locations/types'
import { Box } from '@mui/material'
import { useCallback, useMemo, useState } from 'react'
import MultiLocationSelect from './MultiLocationSelect'

export interface MultiLocationPickerProps {
  value: Location[]
  onChange: (locations: Location[]) => void
}

/**
 * Multi-select location dropdown with the location map underneath. Clicking a
 * marker adds or removes that location, and the map highlights the location
 * selected most recently.
 */
export default function MultiLocationPicker({
  value,
  onChange,
}: MultiLocationPickerProps) {
  const { data, isLoading } = useLatestVersionOfAllLocations()
  const allLocations = useMemo<Location[]>(() => data?.value || [], [data])

  const [filters, setFilters] = useState<Filters>({})
  const [lastSelected, setLastSelected] = useState<Location | null>(null)

  const filteredLocations = useMemo(
    () =>
      allLocations.filter(
        (loc) =>
          (!filters.areaId || loc.areas?.includes(filters.areaId)) &&
          (!filters.regionId || loc.regionId === filters.regionId) &&
          (!filters.locationTypeId ||
            loc.locationTypeId === filters.locationTypeId) &&
          (!filters.measureTypeId ||
            loc.charts?.includes(filters.measureTypeId)) &&
          (!filters.jurisdictionId ||
            loc.jurisdictionId === filters.jurisdictionId)
      ),
    [allLocations, filters]
  )

  const updateFilters = useCallback((newFilters: Partial<Filters>) => {
    setFilters((previous) => ({ ...previous, ...newFilters }))
  }, [])

  // Dropdown changes: highlight whatever was added last, or nothing if empty
  const handleDropdownChange = useCallback(
    (selected: Location[]) => {
      onChange(selected)
      setLastSelected(selected.length ? selected[selected.length - 1] : null)
    },
    [onChange]
  )

  // Marker clicks toggle the location in the selection
  const handleMapClick = useCallback(
    (clicked: Location) => {
      const alreadySelected = value.some(
        (l) => l.locationIdentifier === clicked.locationIdentifier
      )
      if (alreadySelected) {
        const remaining = value.filter(
          (l) => l.locationIdentifier !== clicked.locationIdentifier
        )
        onChange(remaining)
        setLastSelected(remaining.length ? remaining[remaining.length - 1] : null)
      } else {
        onChange([...value, clicked])
        setLastSelected(clicked)
      }
    },
    [value, onChange]
  )

  return (
    <Box>
      <Box sx={{ mb: 2 }}>
        <MultiLocationSelect
          locations={filteredLocations}
          loading={isLoading}
          value={value}
          onChange={handleDropdownChange}
        />
      </Box>
      <SelectLocationMap
        // The map component types locations as the API's search model; it is
        // the same data the other location pickers hand it.
        location={lastSelected as never}
        setLocation={handleMapClick as never}
        locations={allLocations as never}
        filteredLocations={filteredLocations as never}
        filters={filters}
        updateFilters={updateFilters}
      />
    </Box>
  )
}
