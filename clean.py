#!/usr/bin/python
# Copyright 2018 Jacob Trimble
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Cleans the files in the repository.

- Removes any byte-order-marks.
- Ensures the files end in a newline.
"""

import argparse
import os
import subprocess
import sys

ROOT_DIR = os.path.dirname(__file__)


def _GetFiles():
  """Returns a list of files in the repo."""
  data = subprocess.check_output(['git', '-C', ROOT_DIR, 'ls-files'])
  return data.decode('utf8').strip().split('\n')


def _FixFile(path):
  """Fixes the given file."""
  exts = ['.config', '.cs', '.csproj', '.py', '.sln']
  if os.path.splitext(path)[1] not in exts:
    return

  with open(path, 'r') as f:
    data = f.read()
  # Removes any byte-order-mark in the file.
  if data[:3] == '\xef\xbb\xbf':
    data = data[3:]
  # Removes any trailing whitespace and ensures it ends with a newline.
  data = data.strip() + '\n'
  with open(path, 'w') as f:
    f.write(data)


def main(args):
  parser = argparse.ArgumentParser(
      description=__doc__,
      formatter_class=argparse.RawDescriptionHelpFormatter)

  parser.parse_args(args)

  for f in _GetFiles():
    _FixFile(os.path.join(ROOT_DIR, f))
  return 0


if __name__ == '__main__':
  sys.exit(main(sys.argv[1:]))
