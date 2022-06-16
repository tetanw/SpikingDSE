def print_2d_matrix(matrix):
    print('\n'.join([''.join(['{:4}'.format(item) for item in row]) for row in matrix]))

w, h = 4, 4

# Source: https://www.geeksforgeeks.org/zigzag-or-diagonal-traversal-of-matrix/
def zigzag_order(w, h):
    matrix = [[0 for x in range(w)] for y in range(h)]
    prio = w*h

    # There will be ROW+COL-1 lines in the output
    for line in range(1, (h + w)):
        # Get column index of the first element
        # in this line of output. The index is 0
        # for first ROW lines and line - ROW for
        # remaining lines
        start_col = max(0, line - h)

        # Get count of elements in this line.
        # The count of elements is equal to
        # minimum of line number, COL-start_col and ROW
        count = min(line, (w - start_col), h)

        # Print elements of this line
        for j in range(0, count):
            matrix[min(h, line) - j - 1][start_col + j] = prio
            prio = prio - 1

    return matrix

def col_major_order(w, h):
    matrix = [[0 for x in range(w)] for y in range(h)]

    prio = w * h
    for x in range(0, w):
        for y in reversed(range(0, h)):
            matrix[y][x] = prio
            prio = prio - 1

    return matrix

if __name__ == "__main__":
    print("Zigzag:")
    print_2d_matrix(zigzag_order(w, h))
    print("ColMajor:")
    print_2d_matrix(col_major_order(w, h))