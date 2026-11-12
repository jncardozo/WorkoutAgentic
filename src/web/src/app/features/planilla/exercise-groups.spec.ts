import { groupExerciseRows } from './exercise-groups';

describe('groupExerciseRows', () => {
  it('agrupa por nombre sin distinguir mayúsculas, conserva referencias y orden por ejercicio', () => {
    const rows = [
      { id: 1, name: 'Press banca' },
      { id: 2, name: 'Sentadilla' },
      { id: 3, name: ' press BANCA ' },
    ];
    const groups = groupExerciseRows(rows, row => row.name, row => row.id);
    expect(groups.map(group => group.name)).toEqual(['Press banca', 'Sentadilla']);
    expect(groups[0].rows).toEqual([rows[0], rows[2]]);
    expect(groups[0].rows[0]).toBe(rows[0]);
    expect(rows.map(row => row.id)).toEqual([1, 2, 3]);
    expect(rows[2].name).toBe(' press BANCA ');
  });

  it('no combina ejercicios distintos ni inventa nombres para datos pendientes', () => {
    const groups = groupExerciseRows([
      { id: 1, name: '' }, { id: 2, name: 'Press banca' }, { id: 3, name: 'Press inclinado' },
    ], row => row.name, row => row.id);
    expect(groups.map(group => group.name)).toEqual(['sin ejercicio', 'Press banca', 'Press inclinado']);
    expect(groupExerciseRows([], () => '', () => 0)).toEqual([]);
  });
});
