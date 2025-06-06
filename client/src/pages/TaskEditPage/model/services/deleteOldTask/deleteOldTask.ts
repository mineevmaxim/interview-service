import { createAsyncThunk } from '@reduxjs/toolkit';
import { ThunkConfig } from 'app/providers/StoreProvider/config/StateSchema.ts';
import { AxiosError } from 'axios';
import { userActions } from 'entities/User';
import { TaskInfo } from 'entities/Task';

export const deleteOldTask = createAsyncThunk<TaskInfo, string | undefined, ThunkConfig<string>>(
    'taskEditPage/deleteOldTask',
    async (taskId, thunkApi) => {
        const { extra, rejectWithValue, dispatch } = thunkApi;

        if (!taskId) {
            return rejectWithValue('no task id');
        }

        try {
            const response = await extra.api.post(`Tasks/delete?id=${taskId}`);
            if (response.status !== 200) {
                return rejectWithValue('не удалось удалить задачу');
            }

            return response.data;
        } catch (e) {
            const error = e as AxiosError;
            if (error.response?.status === 401) {
                dispatch(userActions.clearToken());
            }
            return rejectWithValue(error.message + ' ' + error.code);
        }
    },
);
