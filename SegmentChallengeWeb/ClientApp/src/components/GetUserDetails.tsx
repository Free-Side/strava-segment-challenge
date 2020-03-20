import * as React from 'react';
import {connect} from "react-redux";
import * as LoginStore from "../store/Login";
import {ApplicationState} from "../store";
import LogoutButton from "./LogoutButton";
import {ChangeEvent} from "react";

type GetUserDetailsProps =
    { login?: LoginStore.LoginState } &
    { setUserProfile: (profile: { birthDate: Date, gender: string }) => void };

type GetUserDetailsState = {
    year?: number,
    // Index by 1 month. Fuck javascript.
    month?: number,
    day?: number,
    gender?: string
};

const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec'
];

const maxBirthYear = new Date().getFullYear() - 13;
const minBirthYear = maxBirthYear - (99 - 13);
let yearArray: number[] = [];
for (let y = maxBirthYear; y >= minBirthYear; y--) {
    yearArray.push(y);
}

function getDaysPerMonth(year: number | undefined, month: number | undefined) {
    if (month) {
        // Note month here is index by 1, where as javascript is index by zero, which is why this works.
        if (year) {
            return new Date(year, month, 0).getDate();
        } else {
            // Intentionally choosing a leap year, since we want to include 29 for february
            return new Date(2020, month, 0).getDate();
        }
    } else {
        return 31;
    }
}

class GetUserDetails extends React.PureComponent<GetUserDetailsProps, GetUserDetailsState> {
    constructor(props: GetUserDetailsProps) {
        super(props);

        const birthDateString = props.login?.loggedInUser?.user_data?.birth_date;
        const birthDateUtc = birthDateString ? new Date(birthDateString) : undefined;

        console.log(props.login?.loggedInUser);

        this.state = {
            year: birthDateUtc?.getUTCFullYear(),
            month: birthDateUtc ? birthDateUtc.getUTCMonth() + 1 : undefined,
            day: birthDateUtc?.getUTCDate(),
            gender: props.login?.loggedInUser?.user_data?.gender
        }
    }

    public render() {
        console.log(this.state);
        return (
            <div className="window-overlay">
                <div className="dialog">
                    <h2>Complete Profile</h2>
                    <p>In order to participate in challenges we need your birth date and gender.</p>
                    <div>
                        <label>Month:
                            <select value={this.state.month} onChange={(e) => this.handleMonthChanged(e)}>
                                <option value={undefined}></option>
                                {months.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                            </select>
                        </label>
                        <label>Day:
                            <select value={this.state.day} onChange={(e) => this.handleDayChanged(e)}>
                                <option value={undefined}></option>
                                {[...Array(getDaysPerMonth(this.state.year, this.state.month))]
                                    .map((_, i) => <option key={i + 1} value={i + 1}>{i + 1}</option>)}
                            </select>
                        </label>
                        <label>Year:
                            <select value={this.state.year} onChange={(e) => this.handleYearChanged(e)}>
                                <option value={undefined}></option>
                                {yearArray.map(y => <option key={y} value={y}>{y}</option>)}
                            </select>
                        </label>
                    </div>
                    <div>
                        <label>Gender:
                            <select value={this.state.gender} onChange={(e) => this.handleGenderChanged(e)}>
                                <option value={undefined}></option>
                                <option value="M">Male</option>
                                <option value="F">Female</option>
                            </select>
                        </label>
                    </div>
                    <div className="flow-row">
                        <LogoutButton/>
                        <button disabled={!(this.state.year && this.state.month && this.state.day && this.state.gender)}
                                onClick={() => this.saveProfile()}>Save
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    private handleYearChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({year: Number(event.target.value)});
    }

    private handleMonthChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({month: Number(event.target.value)});
    }

    private handleDayChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({day: Number(event.target.value)});
    }

    private handleGenderChanged(event: ChangeEvent<HTMLSelectElement>) {
        this.setState({gender: event.target.value});
    }

    private saveProfile() {
        if (this.state.year && this.state.month && this.state.day && this.state.gender) {
            this.props.setUserProfile({
                birthDate: new Date(this.state.year, this.state.month - 1, this.state.day),
                gender: this.state.gender
            });
        }
    }
}

export default connect((state: ApplicationState) => ({login: state.login}), LoginStore.actionCreators)(GetUserDetails);
