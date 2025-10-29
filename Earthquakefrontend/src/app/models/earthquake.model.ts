
export interface Earthquake {
  earthquake_id: number;
  date_time: string;
  title: string;
  mag: number;
  depth: number;
  geojson: GeoJSON.Point;
  isDangerous?: boolean;
  closestLocationName?: string;
}
export interface GeoJson {
  type: string;           // örn: "Point"
  coordinates: [number, number]; // [longitude, latitude]
}
