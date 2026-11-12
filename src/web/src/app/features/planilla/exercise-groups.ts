export interface ExerciseGroup<T> {
  key: number;
  name: string;
  rows: T[];
}

/** Agrupación visual estable; conserva las filas y su orden dentro del ejercicio. */
export function groupExerciseRows<T>(
  rows: readonly T[],
  nameOf: (row: T) => string,
  keyOf: (row: T) => number,
): ExerciseGroup<T>[] {
  const groups = new Map<string, ExerciseGroup<T>>();
  for (const row of rows) {
    const name = nameOf(row).trim() || 'sin ejercicio';
    const identity = name.toLocaleLowerCase('es');
    const group = groups.get(identity) ?? { key: keyOf(row), name, rows: [] };
    group.rows.push(row);
    groups.set(identity, group);
  }
  return [...groups.values()];
}
